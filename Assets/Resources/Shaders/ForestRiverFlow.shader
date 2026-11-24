Shader "StarEco/Forest River Flow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _RiverTex ("River Artwork", 2D) = "gray" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SurfaceSpeed ("Surface Flow", Float) = 0.055
        _DepthSpeed ("Underwater Flow", Float) = 0.018
        [PerRendererData] _Purification ("Purification Transition", Vector) = (0,0,0,0)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="False" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment RiverFragment
            #include "UnitySprites.cginc"

            sampler2D _RiverTex;
            float _SurfaceSpeed;
            float _DepthSpeed;
            float4 _Purification;
            float2 _EnvironmentPreviewClock;

            fixed3 SampleRiver(float x, float y)
            {
                float u = frac(x);
                float mainU = lerp(0.15, 1.0, u);
                fixed3 water = tex2D(_RiverTex, float2(mainU, y)).rgb;
                fixed3 next = tex2D(_RiverTex, float2(saturate(mainU - 0.85), y)).rgb;
                return lerp(water, next, smoothstep(0.82, 1.0, u));
            }

            fixed4 RiverFragment(v2f input) : SV_Target
            {
                float2 uv = input.texcoord;
                float t = lerp(_Time.y, _EnvironmentPreviewClock.y, _EnvironmentPreviewClock.x);
                float crest = 0.635;
                float surface = smoothstep(crest - 0.008, crest + 0.008, uv.y);

                float surfaceX = uv.x * 1.6 - t * _SurfaceSpeed
                    + 0.014 * sin(uv.y * 38.0 + t * 1.6);
                float depthX = uv.x * 1.12 - t * _DepthSpeed
                    + 0.011 * sin(uv.y * 24.0 - t * 0.8);
                fixed3 water = lerp(
                    SampleRiver(depthX, min(uv.y, crest)),
                    SampleRiver(surfaceX, max(uv.y, crest)), surface);

                // Sparse ripples over the surface, with slower motion beneath it.
                float wave = sin(surfaceX * 23.0 + uv.y * 123.0 + 1.3 * sin(uv.y * 16.0));
                float ripple = smoothstep(0.94, 1.0, wave) * surface;
                ripple *= smoothstep(0.12, 0.9, sin(surfaceX * 43.0 - uv.y * 19.0));
                water += ripple * fixed3(0.013, 0.020, 0.016);

                float clarity = lerp(_Purification.x, _Purification.y,
                    smoothstep(0.0, 0.7, t - _Purification.z));
                fixed lum = dot(water, fixed3(0.2126, 0.7152, 0.0722));
                fixed3 cleared = lum * fixed3(0.78, 1.09, 1.22)
                    + fixed3(0.009, 0.027, 0.039) * (0.5 + 0.5 * surface);
                water = lerp(water, cleared, clarity);
                fixed alpha = input.color.a;
                return fixed4(water * input.color.rgb * alpha, alpha);
            }
            ENDCG
        }
    }
}
