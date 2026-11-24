Shader "StarEco/Tree Wind"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Sway ("Wind Sway", Range(0,0.35)) = 0.22
        _WindSpeed ("Wind Speed", Range(0,3)) = 1
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="False" "DisableBatching"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex TreeVert
            #pragma fragment SpriteFrag
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            float _Sway;
            float _WindSpeed;
            float2 _EnvironmentPreviewClock;

            v2f TreeVert(appdata_t input)
            {
                float2 origin = mul(unity_ObjectToWorld, float4(0,0,0,1)).xy;
                float phase = dot(origin, float2(1.67, 0.71));
                float height = smoothstep(0.19, 1.0, input.texcoord.y);
                float time = lerp(_Time.y, _EnvironmentPreviewClock.y, _EnvironmentPreviewClock.x) * _WindSpeed;
                float gust = sin(time * 1.45 + phase)
                           + 0.18 * sin(time * 2.63 + phase * 1.7);
                input.vertex.x += _Sway * height * height * gust;
                return SpriteVert(input);
            }
            ENDCG
        }
    }
}
