Shader "Custom/cloudShadows"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}
        _PerlinNoise ("Perlin Noise (R)", 2D) = "white" {}
        _WorleyNoise ("Worley Noise (R)", 2D) = "white" {}
        
        _tiling("Tiling", Vector) = (1, 1.5, 0, 0)
        _scale("Scale", Float) = 0.6
        _alpha("Alpha", Float) = 0.9
        _cloudAlphaMax("Cloud Alpha Max", Float) = 0.62

        _speed1("Speed 1", Float) = 10
        _speed2("Speed 2", Float) = 6
        _cloudsDir1("Clouds Dir 1", Vector) = (1.0, 0.0, 0, 0)
        _cloudsDir2("Clouds Dir 2", Vector) = (1.0, 0.2, 0, 0)

        _cloudColor("Cloud Color", Color) = (0, 0, 0, 1)
        [Toggle] _step("Smooth Shading", Int) = 0

        _sunDirection("Sun Direction", Vector) = (1.0, -1.0, 1.0, 0)

        _fastMode("Fast Mode", Int) = 0
        _cloudMode("Clouds Mode", Int) = 2

        _ma1("Matrix A1", Vector) = (1.6, -1.2, 0, 0)
        _ma2("Matrix A2", Vector) = (1.2, 1.6, 0, 0)
        
        // Required for SpriteRenderer
        _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip("Flip", Vector) = (1,1,1,1)
    }

    SubShader
    {
        Tags 
        { 
            "Queue" = "Transparent" 
            "RenderType" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                half4  color        : COLOR;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_PerlinNoise);
            SAMPLER(sampler_PerlinNoise);
            TEXTURE2D(_WorleyNoise);
            SAMPLER(sampler_WorleyNoise);

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _Color;
            float4 _RendererColor;
            float2 _tiling;
            float _scale;
            float _alpha;
            float _cloudAlphaMax;
            float _speed1;
            float _speed2;
            float2 _cloudsDir1;
            float2 _cloudsDir2;
            float3 _cloudColor;
            float3 _sunDirection;
            float2 _ma1;
            float2 _ma2;
            float _scrollSpeed;
            float2 _cameraSize;
            float2 _pos;
            int _step;
            int _fastMode;
            int _cloudMode;
            CBUFFER_END

            // --- Noise Functions ---
            float2 hash21(float2 p) {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
            }

            float2 hash22(float2 p) {
                float3 p3 = frac(p.xyx * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 19.19);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            float2 randomVec2(float i, float j) { return float2(i, j) + hash22(float2(i, j)); }

            float fastWorley(float2 p, float scale) { 
                return SAMPLE_TEXTURE2D(_WorleyNoise, sampler_WorleyNoise, p / 24 / scale).r; 
            }

            float worley(float2 uv, float scale) {
                float2 ij = floor(uv / scale);
                float minDist = 2.0;
                for (float x = -1.0; x <= 1.0; x++) {
                    for (float y = -1.0; y <= 1.0; y++) {
                        float d = length(randomVec2(ij.x + x, ij.y + y) * scale - uv);
                        minDist = min(minDist, d);
                    }
                }
                return 1.0 - minDist / scale;
            }

            float cloudWorley(float2 uv, float scale, float2 dir, float2 dir2, float speed1, float speed2) {
                float t = _Time.y;
                float2 uv1 = uv + normalize(dir) * 0.01 * t * speed1;
                float c1 = (_fastMode ? fastWorley(uv1, 0.2 * scale) : worley(uv1, 0.2 * scale)) * 0.15;
                float c2 = (_fastMode ? fastWorley(uv1, 0.72 * scale) : worley(uv1, 0.72 * scale)) * 0.8;
                return saturate((c1 + c2) / 0.95);
            }

            float4 cloudWorleyNorm(float2 uv, float scale, float3 sunDir, float3 col, float alpha) {
                float res = cloudWorley(uv, scale, _cloudsDir1, _cloudsDir2, _speed1, _speed2);
                float3 slope = float3(0.01, 0, 0);
                float px = cloudWorley(uv + slope.xy, scale, _cloudsDir1, _cloudsDir2, _speed1, _speed2);
                float py = cloudWorley(uv + slope.yx, scale, _cloudsDir1, _cloudsDir2, _speed1, _speed2);
                float3 norm = normalize(float3(px - res, py - res, -0.1));
                float light = dot(norm, -normalize(sunDir));
                return float4(col, alpha * saturate(light)) * res;
            }

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                // Standard Sprite Color behavior (Tint * Renderer Color)
                o.color = v.color * _Color * _RendererColor;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Calculate scrolling UVs
                float2 uv = i.uv + (_pos / _cameraSize * _scrollSpeed);
                uv *= _tiling;

                float4 cloudOut = float4(0, 0, 0, 0);
                
                if(_cloudMode == 2) 
                    cloudOut = cloudWorleyNorm(uv, _scale, _sunDirection, _cloudColor, _alpha);
                else 
                    cloudOut = float4(_cloudColor, cloudWorley(uv, _scale, _cloudsDir1, _cloudsDir2, _speed1, _speed2) * _alpha);

                // Apply smoothstep logic
                if(_step != 0) 
                    cloudOut.a = smoothstep(0, min(_alpha, _cloudAlphaMax), cloudOut.a);
                
                cloudOut.a = clamp(cloudOut.a, 0, _cloudAlphaMax);

                // Combine with vertex color (Sprite Renderer tint)
                half4 finalColor = cloudOut * i.color;
                
                return finalColor;
            }
            ENDHLSL
        }
    }
    Fallback "Sprites/Default"
}