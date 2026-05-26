using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DGLab.BepInEx
{
    public static class DGLabBodyBinding
    {
        public static float GetBoundPain(Body body, string binding)
        {
            if (body == null || body.limbs == null) return 0f;
            if (string.IsNullOrWhiteSpace(binding)) return 0f;

            var max = 0f;
            var parts = binding.Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                foreach (var index in ExpandLimbIndices(part.Trim()))
                {
                    if (index < 0 || index >= body.limbs.Length) continue;

                    var limb = body.limbs[index];
                    if (limb == null || limb.dismembered) continue;
                    max = Mathf.Max(max, limb.pain);
                }
            }

            return max;
        }

        public static bool IsLimbBound(string binding, int index)
        {
            if (string.IsNullOrWhiteSpace(binding)) return false;

            var parts = binding.Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                foreach (var boundIndex in ExpandLimbIndices(part.Trim()))
                {
                    if (boundIndex == index) return true;
                }
            }

            return false;
        }

        public static bool BindingContainsToken(string binding, string token)
        {
            var tokenIndices = ExpandLimbIndices(token);
            if (tokenIndices.Length == 0) return false;
            return tokenIndices.All(index => IsLimbBound(binding, index));
        }

        public static string ToggleBindingToken(string binding, string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return string.IsNullOrWhiteSpace(binding) ? string.Empty : binding.Trim();

            var tokenIndices = ExpandLimbIndices(token);
            if (tokenIndices.Length == 0) return string.IsNullOrWhiteSpace(binding) ? string.Empty : binding.Trim();

            var selected = new SortedSet<int>();
            if (!string.IsNullOrWhiteSpace(binding))
            {
                var existing = binding.Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in existing)
                {
                    foreach (var index in ExpandLimbIndices(part.Trim())) selected.Add(index);
                }
            }

            var alreadySelected = tokenIndices.All(index => selected.Contains(index));
            foreach (var index in tokenIndices)
            {
                if (alreadySelected) selected.Remove(index);
                else selected.Add(index);
            }

            return FormatBinding(selected);
        }

        private static string FormatBinding(IEnumerable<int> indices)
        {
            var tokens = new List<string>();
            foreach (var index in indices)
            {
                var token = TokenForIndex(index);
                if (!string.IsNullOrEmpty(token)) tokens.Add(token);
            }
            return string.Join(",", tokens.ToArray());
        }

        private static string TokenForIndex(int index)
        {
            switch (index)
            {
                case 0: return "Head";
                case 1: return "UpTorso";
                case 2: return "DownTorso";
                case 3: return "LeftUpperArm";
                case 4: return "LeftForearm";
                case 5: return "LeftHand";
                case 6: return "RightUpperArm";
                case 7: return "RightForearm";
                case 8: return "RightHand";
                case 9: return "LeftThigh";
                case 10: return "LeftLowerLeg";
                case 11: return "LeftFoot";
                case 12: return "RightThigh";
                case 13: return "RightLowerLeg";
                case 14: return "RightFoot";
                default: return null;
            }
        }

        private static int[] ExpandLimbIndices(string value)
        {
            if (TryParseLimbGroup(value, out var indices)) return indices;
            if (TryParseLimbIndex(value, out var index)) return new[] { index };
            return new int[0];
        }

        private static bool TryParseLimbGroup(string value, out int[] indices)
        {
            indices = null;
            if (string.IsNullOrWhiteSpace(value)) return false;

            switch (value.ToLowerInvariant())
            {
                case "upperbody":
                case "upper":
                case "上半身": indices = new[] { 0, 1, 3, 4, 5, 6, 7, 8 }; return true;
                case "lowerbody":
                case "lower":
                case "下半身": indices = new[] { 2, 9, 10, 11, 12, 13, 14 }; return true;
                case "arms":
                case "botharms":
                case "双臂": indices = new[] { 3, 4, 5, 6, 7, 8 }; return true;
                case "hands":
                case "bothhands":
                case "双手": indices = new[] { 5, 8 }; return true;
                case "legs":
                case "bothlegs":
                case "双腿": indices = new[] { 9, 10, 11, 12, 13, 14 }; return true;
                case "feet":
                case "bothfeet":
                case "双脚": indices = new[] { 11, 14 }; return true;
                case "torso":
                case "body":
                case "躯干": indices = new[] { 1, 2 }; return true;
                case "chest":
                case "胸":
                case "胸部": indices = new[] { 1 }; return true;
                case "hips":
                case "hip":
                case "abdomen":
                case "股":
                case "腹":
                case "腹部": indices = new[] { 2 }; return true;
                case "leftarm":
                case "左臂": indices = new[] { 3, 4, 5 }; return true;
                case "rightarm":
                case "右臂": indices = new[] { 6, 7, 8 }; return true;
                case "leftleg":
                case "左腿": indices = new[] { 9, 10, 11 }; return true;
                case "rightleg":
                case "右腿": indices = new[] { 12, 13, 14 }; return true;
                default: return false;
            }
        }

        public static bool TryParseLimbIndex(string value, out int index)
        {
            index = 0;
            if (int.TryParse(value, out index)) return true;
            if (string.IsNullOrWhiteSpace(value)) return false;

            switch (value.ToLowerInvariant())
            {
                case "head":
                case "头": index = 0; return true;
                case "uptorso":
                case "uppertorso":
                case "chest":
                case "胸":
                case "胸部": index = 1; return true;
                case "downtorso":
                case "lowertorso":
                case "abdomen":
                case "hip":
                case "hips":
                case "pelvis":
                case "股":
                case "腹":
                case "腹部": index = 2; return true;
                case "leftupperarm":
                case "左上臂": index = 3; return true;
                case "leftforearm":
                case "左前臂": index = 4; return true;
                case "lefthand":
                case "左手": index = 5; return true;
                case "rightupperarm":
                case "右上臂": index = 6; return true;
                case "rightforearm":
                case "右前臂": index = 7; return true;
                case "righthand":
                case "右手": index = 8; return true;
                case "leftthigh":
                case "左大腿": index = 9; return true;
                case "leftshin":
                case "leftcalf":
                case "leftlowerleg":
                case "左小腿": index = 10; return true;
                case "leftfoot":
                case "左脚": index = 11; return true;
                case "rightthigh":
                case "右大腿": index = 12; return true;
                case "rightshin":
                case "rightcalf":
                case "rightlowerleg":
                case "右小腿": index = 13; return true;
                case "rightfoot":
                case "右脚": index = 14; return true;
                default: return false;
            }
        }
    }
}
