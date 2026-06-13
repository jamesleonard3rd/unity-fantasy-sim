// Full refactored LightingSystemEditor with enum-based light modes

using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEngine.Rendering.Universal;

namespace Modern2D
{
#if UNITY_EDITOR

    public class ColorUtils
    {
        public static Color HexToRGB(string hex)
        {
            float r = (((IsNumeric(hex[0]) ? (int)(hex[0] - '0') : (int)(hex[0] - 'A' + 11)) * 16) + (IsNumeric(hex[1]) ? (int)(hex[1] - '0') : (int)(hex[1] - 'A'))) / 255f;
            float g = (((IsNumeric(hex[2]) ? (int)(hex[2] - '0') : (int)(hex[2] - 'A' + 11)) * 16) + (IsNumeric(hex[3]) ? (int)(hex[3] - '0') : (int)(hex[3] - 'A'))) / 255f;
            float b = (((IsNumeric(hex[4]) ? (int)(hex[4] - '0') : (int)(hex[4] - 'A' + 11)) * 16) + (IsNumeric(hex[5]) ? (int)(hex[5] - '0') : (int)(hex[5] - 'A'))) / 255f;
            return new Color(r, g, b);
        }

        public static bool IsNumeric(char c)
        {
            return (c >= '0' && c <= '9');
        }
    }

    [CustomEditor(typeof(LightingSystem))]
    public class LightingSystemEditor : Editor
    {
        [SerializeField] private Texture2D myTexture;

        private enum LightCutoffPreset
        {
            Early_Hard,
            Early_Medium,
            Early_Soft,

            Mid_Hard,
            Mid_Medium,
            Mid_Soft,

            Late_Hard,
            Late_Medium,
            Late_Soft,

            VeryLate_Medium,
            VeryLate_Soft,
            FullRange_Soft
        }

        private enum LightStrengthRange
        {
            Range_0_1,
            Range_0_2,
            Range_0_4
        }

        private bool AutoLightBoundsEnabled
        {
            get => EditorPrefs.GetBool(LightingSystem.system.AutoLightBoundsPrefKey, false);
            set => EditorPrefs.SetBool(LightingSystem.system.AutoLightBoundsPrefKey, value);
        }

        private LightCutoffPreset SelectedPreset
        {
            get => (LightCutoffPreset)EditorPrefs.GetInt(LightingSystem.system.PresetKey, 0);
            set => EditorPrefs.SetInt(LightingSystem.system.PresetKey, (int)value);
        }

        private LightStrengthRange SelectedRange
        {
            get => (LightStrengthRange)EditorPrefs.GetInt(LightingSystem.system.StrengthRangeKey, 2);
            set => EditorPrefs.SetInt(LightingSystem.system.StrengthRangeKey, (int)value);
        }

        private bool UsePresets
        {
            get => LightingSystem.system.UsePresetsKey; 
            set => LightingSystem.system.UsePresetsKey = value; 
        }
        public Color GetFieldColor(Component c, out Color bgc)
        {
            bgc = GUI.backgroundColor;
            if (c == null) return Color.red;
            else return GUI.backgroundColor;
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();

            GUIStyle style = new GUIStyle
            {
                stretchWidth = true,
                stretchHeight = true,
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label(myTexture, style);
            GUILayout.Space(10);

            GUIStyle header = new GUIStyle
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = ColorUtils.HexToRGB("EDAE49") }
            };

            GUILayout.Space(20);
            GUILayout.Label("OPTIONS", header, GUILayout.ExpandWidth(true));

            LightingSystem system = (LightingSystem)target;

            GUILayout.Space(5); system._shadowColor.value = EditorGUILayout.ColorField("Ambient Color", system._shadowColor.value);
            GUILayout.Space(5); system._shadowReflectiveness.value = EditorGUILayout.Slider("Shadow Reflectiveness", system._shadowReflectiveness.value, 0, 1);
            GUILayout.Space(5); system._shadowAlpha.value = EditorGUILayout.Slider("Shadow Alpha", system._shadowAlpha.value, 0, 1);
            GUILayout.Space(5); system._shadowLength.value = EditorGUILayout.Slider("Shadow Length", system._shadowLength.value, 0, 5);
            GUILayout.Space(5); system._shadowNarrowing.value = EditorGUILayout.Slider("Shadow Narrowing", system._shadowNarrowing.value, 0, 1);
            GUILayout.Space(5); system._shadowFalloff.value = EditorGUILayout.Slider("Shadow Falloff", system._shadowFalloff.value, 0, 15);
            GUILayout.Space(5); system._shadowAngle.value = EditorGUILayout.Slider("Shadow Angle", system._shadowAngle.value, 0, 90);
            GUILayout.Space(5); system.ShadowsLayerName = EditorGUILayout.TextField("default shadow sorting layer", system.ShadowsLayerName);

            var v = system.noBlending.value;
            GUILayout.Space(5); system.noBlending.value = EditorGUILayout.Toggle("disable shadow blending ", system.noBlending.value);
            if (v != system.noBlending.value)
            {
                DeleteAllShadows();
                CreateAllShadows(system);
            }

            if (!system._useClosestPointLightForDirection || system.isLightDirectional.value)
            {
                GUILayout.Space(5); system._onlyRenderIn2DLight.value = EditorGUILayout.Toggle("Render In URP 2D Light Only", system._onlyRenderIn2DLight.value);

                if (system._onlyRenderIn2DLight)
                {
                    system._minimumAlphaOutOfLight.value = EditorGUILayout.Slider("Minimum alpha", system._minimumAlphaOutOfLight.value, 0f, 4f);
                    system._maximumAlphaOutOfLight.value = EditorGUILayout.Slider("Maximum alpha", system._maximumAlphaOutOfLight.value, 0f, 4f);
                    system._2dLightsShadowStrength.value = EditorGUILayout.FloatField("Strength in light", system._2dLightsShadowStrength.value);
                }
            }
            else
            {
                system._onlyRenderIn2DLight.value = true;
            }

            GUILayout.Space(10);
            Color backc; GUI.backgroundColor = GetFieldColor(system.followPlayer, out backc);
            system.followPlayer = (Transform)EditorGUILayout.ObjectField("Camera Transform", system.followPlayer, typeof(Transform), true);
            GUI.backgroundColor = backc;

            GUILayout.Space(20);
            GUILayout.Label("BLUR", header, GUILayout.ExpandWidth(true));


            GUILayout.Space(5);
            system.enableBlur.value = GUILayout.Toggle(system.enableBlur.value, "enable blur");
            system.blurSampleSize.value = EditorGUILayout.IntSlider("sampling area", system.blurSampleSize.value, 1, 32);
            system.blurStrength.value = EditorGUILayout.Slider("blur strength", system.blurStrength.value, 0f, 1f);
            system.blurDirection.value = EditorGUILayout.Vector2Field("blur direction", system.blurDirection.value);

            GUILayout.Space(15);
            header.normal.textColor = ColorUtils.HexToRGB("D1495B");
            GUILayout.Label("LIGHT SOURCE", header, GUILayout.ExpandWidth(true));

            system.SetCallbacks();
            system.Singleton();



            system.lightMode = (LightMode)EditorGUILayout.EnumPopup("Light Mode", system.lightMode);

            switch (system.lightMode)
            {
                case LightMode.Directional:
                    system.isLightDirectional.value = true;
                    system._useClosestPointLightForDirection.value = false;
                    DirectionalFields(system);
                    break;

                case LightMode.SinglePoint:
                    system.isLightDirectional.value = false;
                    system._useClosestPointLightForDirection.value = false;
                    SourceFields_SinglePoint(system);
                    break;

                case LightMode.Unity2D_Persistent:
                    system.isLightDirectional.value = false;
                    system._useClosestPointLightForDirection.value = true;
                    SourceFields_2D_Persistent(system);
                    break;

                case LightMode.Unity2D_Realistic:
                    system.isLightDirectional.value = false;
                    system._useClosestPointLightForDirection.value = true;
                    SourceFields_2D_Realistic(system);
                    break;
            }

            GUILayout.Space(15);
            header.normal.textColor = ColorUtils.HexToRGB("08B2E3");
            GUILayout.Label("SHADOW SPRITE PIVOT", header, GUILayout.ExpandWidth(true));
            system._useSpritePivotForShadowPivot.value = GUILayout.Toggle(system._useSpritePivotForShadowPivot.value, " Use sprite-pivot as default shadow-pivot ");

            GUILayout.Space(15);
            header.normal.textColor = ColorUtils.HexToRGB("57A773");
            GUILayout.Label("SHADOW SPRITE FLIP-X", header, GUILayout.ExpandWidth(true));
            system.defaultShadowSprflipx = GUILayout.Toggle(system.defaultShadowSprflipx, "default shadow sprite flip-x");
            system.shadowSprFlip = GUILayout.Toggle(system.shadowSprFlip, "flips shadow sprite horizontaly based on orientation");

            GUILayout.Space(15);
            base.OnInspectorGUI();

            GUILayout.Space(15);
            if (GUILayout.Button("DELETE ALL SHADOWS")) DeleteAllShadows();
            if (GUILayout.Button("CREATE ALL SHADOWS")) CreateAllShadows(system);
            if (GUILayout.Button("UPDATE ALL SHADOWS"))
            {
                system.extendedUpdateThisFrame = true;
                system.OnShadowSettingsChanged();
                system.UpdateShadows(Transform.FindObjectsOfType<StylizedShadowCaster2D>().ToDictionary(t => t.transform, t => t.shadowData.shadow));
            }

            SetLayers();

            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(system);
        }

        private static void ApplyPreset(
                    LightCutoffPreset preset,
                    LightStrengthRange range,
                    LightingSystem system
        )
        {
            float max = range switch
            {
                LightStrengthRange.Range_0_1 => 1f,
                LightStrengthRange.Range_0_2 => 2f,
                _ => 4f
            };

            float min;
            float strength;

            switch (preset)
            {
                // EARLY
                case LightCutoffPreset.Early_Hard:
                    min = max * 0.05f; strength = 0.9f; break;
                case LightCutoffPreset.Early_Medium:
                    min = max * 0.10f; strength = 0.6f; break;
                case LightCutoffPreset.Early_Soft:
                    min = max * 0.15f; strength = 0.35f; break;

                // MID
                case LightCutoffPreset.Mid_Hard:
                    min = max * 0.30f; strength = 0.85f; break;
                case LightCutoffPreset.Mid_Medium:
                    min = max * 0.40f; strength = 0.55f; break;
                case LightCutoffPreset.Mid_Soft:
                    min = max * 0.50f; strength = 0.30f; break;

                // LATE
                case LightCutoffPreset.Late_Hard:
                    min = max * 0.65f; strength = 0.8f; break;
                case LightCutoffPreset.Late_Medium:
                    min = max * 0.75f; strength = 0.5f; break;
                case LightCutoffPreset.Late_Soft:
                    min = max * 0.85f; strength = 0.25f; break;

                // EXTREME
                case LightCutoffPreset.VeryLate_Medium:
                    min = max * 0.92f; strength = 0.45f; break;
                case LightCutoffPreset.VeryLate_Soft:
                    min = max * 0.96f; strength = 0.25f; break;

                default: // FullRange_Soft
                    min = 0f; strength = 0.2f; break;
            }

            system._minimumAlphaOutOfLight.value = min;
            system._maximumAlphaOutOfLight.value = max;
          
        }

        private void CreateAllShadows(LightingSystem system)
        {
            foreach (var s in Transform.FindObjectsOfType<StylizedShadowCaster2D>())
            {
                s.RebuildShadow();
                system.AddShadow(s.shadowData);
                system.extendedUpdateThisFrame = true;
                system.OnShadowSettingsChanged();
                system.UpdateShadows(Transform.FindObjectsOfType<StylizedShadowCaster2D>().ToDictionary(t => t.transform, t => t.shadowData.shadow));
            }
        }

        private void DeleteAllShadows()
        {
            foreach (var s in GameObject.FindGameObjectsWithTag("Shadow"))
                DestroyImmediate(s);
        }

        private void DirectionalFields(LightingSystem system)
        {
            GUILayout.Label("direction:");
            GUILayout.Space(20);
            system.directionalLightAngle.value = GUILayout.HorizontalSlider(system.directionalLightAngle.value, 0, 359);
            GUILayout.Space(20);
        }

        private void SetLayers()
        {
            if (!Layers.LayerExists("LightingSystem"))
                if (!Layers.CreateLayer("LightingSystem"))
                    Debug.LogError("Not enough space for the LightingSystem layer");

            if (!Layers.LayerExists("2DWater"))
                if (!Layers.CreateLayer("2DWater"))
                    Debug.LogError("Not enough space for the Water layer");

            if (!Layers.TagExists("Shadow"))
                if (!Layers.CreateTag("Shadow"))
                    Debug.LogError("Not enough space for the Shadow tag");
        }

        private void SourceFields_2D_Persistent(LightingSystem system)
        {
            system._2dLightsMask.value = EditorGUILayout.Toggle("use 2d lights layer mask", system._2dLightsMask.value);
            if (system._2dLightsMask.value)
                system._2dLightsLayer.value = EditorGUILayout.LayerField("2d lights layer mask", system._2dLightsLayer.value);

            system._2dLightsShadowStrength.value = EditorGUILayout.FloatField("Strength in light", system._2dLightsShadowStrength.value);

            GUILayout.Space(5);
            system.distMinMax.value = EditorGUILayout.Vector2Field("shadow distance min max", system.distMinMax.value);
            GUILayout.Space(5);
            system.shadowLengthMinMax.value = EditorGUILayout.Vector2Field("shadow length multiplier min max", system.shadowLengthMinMax.value);
            system.shadowsMaterial.SetInt("_onlyRenderIn2DLight", 0);
        }

        private void SourceFields_2D_Realistic(LightingSystem system)
        {
            system._2dLightsMask.value = EditorGUILayout.Toggle("use 2d lights layer mask", system._2dLightsMask.value);
            if (system._2dLightsMask.value)
                system._2dLightsLayer.value = EditorGUILayout.LayerField("2d lights layer mask", system._2dLightsLayer.value);


            GUILayout.Label("");

          GUILayout.Label("Light values setup:");

            EditorGUI.BeginChangeCheck();
            UsePresets = EditorGUILayout.Toggle("Use presets", UsePresets);
            bool modeChanged = EditorGUI.EndChangeCheck();

            if (UsePresets)
            {
                EditorGUI.BeginChangeCheck();

                SelectedRange = (LightStrengthRange)EditorGUILayout.EnumPopup(
                    "Intensity range",
                    SelectedRange
                );

                SelectedPreset = (LightCutoffPreset)EditorGUILayout.EnumPopup(
                    "Shadow cutoff preset",
                    SelectedPreset
                );

                if (EditorGUI.EndChangeCheck() || modeChanged)
                {
                    ApplyPreset(SelectedPreset, SelectedRange, system);
                }

               
            }
            else
            {
                GUILayout.Label("Manual values");

                system._minimumAlphaOutOfLight.value =
                    EditorGUILayout.Slider(
                        "minimum light intensity where shadow appear",
                        system._minimumAlphaOutOfLight.value,
                        0f,
                        4f
                    );

                system._maximumAlphaOutOfLight.value =
                    EditorGUILayout.Slider(
                        "maximum light intensity in scene",
                        system._maximumAlphaOutOfLight.value,
                        0f,
                        4f
                    );

              
            }

            system._2dLightsShadowStrength.value =
                  EditorGUILayout.FloatField(
                      "Strength in light",
                      system._2dLightsShadowStrength.value
                  );

            GUILayout.Space(5);
            system.distMinMax.value = EditorGUILayout.Vector2Field("shadow distance min max", system.distMinMax.value);
            GUILayout.Space(5);
            system.shadowLengthMinMax.value = EditorGUILayout.Vector2Field("shadow length multiplier min max", system.shadowLengthMinMax.value);
            system.shadowsMaterial.SetInt("_onlyRenderIn2DLight", 1);
        }

        private void SourceFields_SinglePoint(LightingSystem system)
        {
            Color backc;
            GUI.backgroundColor = GetFieldColor(system.source, out backc);
            system.source = (Transform)EditorGUILayout.ObjectField("Light Source", system.source, typeof(Transform), true);
            GUI.backgroundColor = backc;

            GUILayout.Space(5);
            system.distMinMax.value = EditorGUILayout.Vector2Field("shadow distance min max", system.distMinMax.value);
            GUILayout.Space(5);
            system.shadowLengthMinMax.value = EditorGUILayout.Vector2Field("shadow length multiplier min max", system.shadowLengthMinMax.value);
        }
    }
#endif
}