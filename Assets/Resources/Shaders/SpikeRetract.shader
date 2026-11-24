Shader "StarEco/Spike Retract"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [PerRendererData] _Retraction ("Retraction", Range(0,1)) = 0
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
            #pragma fragment SpikeFragment
            #include "UnitySprites.cginc"
            float _Retraction;

            fixed4 SpikeFragment(v2f input) : SV_Target
            {
                float2 uv = input.texcoord;
                // Keep the original metal lip in place while the blades sink behind it.
                float slotTop = 19.0 / 160.0;
                if (uv.y > slotTop)
                {
                    uv.y += (1.0 - slotTop) * _Retraction;
                    if (uv.y >= 1.0) return 0;
                }
                fixed4 color = SampleSpriteTexture(uv) * input.color;
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
