Shader "StarEco/Forest Grass Wind"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Sway ("Grass Sway", Range(0,0.035)) = 0.018
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="False" "DisableBatching"="True" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment GrassFragment
            #include "UnitySprites.cginc"

            float _Sway;
            float2 _EnvironmentPreviewClock;

            fixed4 GrassFragment(v2f input) : SV_Target
            {
                float2 uv = input.texcoord;
                // The forest tile has grass painted into the top quarter.
                float aboveRoots = smoothstep(0.755, 0.99, uv.y);
                float edge = smoothstep(0.0, 0.06, min(uv.x, 1.0 - uv.x));
                float phase = unity_ObjectToWorld._m03 * 0.27 + uv.x * 17.0;
                float time = lerp(_Time.y, _EnvironmentPreviewClock.y, _EnvironmentPreviewClock.x);
                float wind = sin(time * 1.9 + phase) + 0.2 * sin(time * 3.4 + phase * 0.7);
                uv.x -= wind * _Sway * aboveRoots * edge;

                fixed4 color = SampleSpriteTexture(uv) * input.color;
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
