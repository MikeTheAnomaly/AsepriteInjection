using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor.Animations;
using UnityEditor.AssetImporters;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

namespace GhostInTheHall.AsepriteInjection
{
    internal static class AnimationPostProcessor
    {
        public static void Run(AsepriteImporter.ImportEventArgs args)
        {
            var importedObjects = new List<Object>();
            args.context.GetObjects(importedObjects);

            var animatorController = importedObjects.OfType<AnimatorController>().FirstOrDefault();
            if (animatorController == null)
            {
                return;
            }

            animatorController.AddParameter("direction", AnimatorControllerParameterType.Float);
            if (animatorController.layers.Length == 0)
            {
                return;
            }

            var baseLayer = animatorController.layers[0];
            var stateMachine = baseLayer.stateMachine;
            var animationClips = importedObjects.OfType<AnimationClip>().ToList();
            var animationGroups = GroupAnimationsByBaseName(animationClips);

            foreach (var group in animationGroups.Where(g => g.Value.Count > 1))
            {
                CreateBlendTreeForGroup(animatorController, stateMachine, group.Key, group.Value, args.context);
            }

            foreach (var group in animationGroups.Where(g => g.Value.Count == 1))
            {
                var state = stateMachine.AddState(group.Key);
                state.motion = group.Value[0];
            }
        }

        private static Dictionary<string, List<AnimationClip>> GroupAnimationsByBaseName(List<AnimationClip> clips)
        {
            var groups = new Dictionary<string, List<AnimationClip>>();

            foreach (var clip in clips)
            {
                var match = Regex.Match(clip.name, @"^(.+)_(\d+)deg$");
                var baseName = match.Success ? match.Groups[1].Value : clip.name;

                if (!groups.TryGetValue(baseName, out var group))
                {
                    group = new List<AnimationClip>();
                    groups[baseName] = group;
                }

                group.Add(clip);
            }

            return groups;
        }

        private static void CreateBlendTreeForGroup(
            AnimatorController controller,
            AnimatorStateMachine stateMachine,
            string baseName,
            List<AnimationClip> clips,
            AssetImportContext context)
        {
            var blendTree = new BlendTree
            {
                name = baseName + "_BlendTree",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "direction",
                useAutomaticThresholds = false,
            };

            var sortedClips = clips
                .Select(clip => new
                {
                    Clip = clip,
                    Degree = ExtractDegreeFromName(clip.name),
                })
                .OrderBy(x => x.Degree)
                .ToList();

            var children = new ChildMotion[sortedClips.Count];
            for (var i = 0; i < sortedClips.Count; i++)
            {
                children[i] = new ChildMotion
                {
                    motion = sortedClips[i].Clip,
                    threshold = i == 0 ? 0f : (float)i / sortedClips.Count,
                    timeScale = 1f,
                };
            }

            blendTree.children = children;
            context.AddObjectToAsset(blendTree.name, blendTree);

            var state = stateMachine.AddState(baseName);
            state.motion = blendTree;
        }

        private static float ExtractDegreeFromName(string clipName)
        {
            var match = Regex.Match(clipName, @"_(\d+)deg$");
            return match.Success && float.TryParse(match.Groups[1].Value, out var degree) ? degree : 0f;
        }
    }
}
