using System;
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
                case "armf":
                case "frontarm":
                case "leftarm":
                case "左臂": indices = new[] { 3, 4, 5 }; return true;
                case "armb":
                case "backarm":
                case "rightarm":
                case "右臂": indices = new[] { 6, 7, 8 }; return true;
                case "legf":
                case "frontleg":
                case "leftleg":
                case "左腿": indices = new[] { 9, 10, 11 }; return true;
                case "legb":
                case "backleg":
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
                case "armfupper":
                case "frontupperarm":
                case "leftupperarm":
                case "左上臂": index = 3; return true;
                case "armflower":
                case "frontforearm":
                case "leftforearm":
                case "左前臂": index = 4; return true;
                case "handf":
                case "fronthand":
                case "lefthand":
                case "左手": index = 5; return true;
                case "armbupper":
                case "backupperarm":
                case "rightupperarm":
                case "右上臂": index = 6; return true;
                case "armblower":
                case "backforearm":
                case "rightforearm":
                case "右前臂": index = 7; return true;
                case "handb":
                case "backhand":
                case "righthand":
                case "右手": index = 8; return true;
                case "legfupper":
                case "frontthigh":
                case "leftthigh":
                case "左大腿": index = 9; return true;
                case "legflower":
                case "frontshin":
                case "frontcalf":
                case "leftshin":
                case "leftcalf":
                case "左小腿": index = 10; return true;
                case "footf":
                case "frontfoot":
                case "leftfoot":
                case "左脚": index = 11; return true;
                case "legbupper":
                case "backthigh":
                case "rightthigh":
                case "右大腿": index = 12; return true;
                case "legblower":
                case "backshin":
                case "backcalf":
                case "rightshin":
                case "rightcalf":
                case "右小腿": index = 13; return true;
                case "footb":
                case "backfoot":
                case "rightfoot":
                case "右脚": index = 14; return true;
                default: return false;
            }
        }
    }
}
