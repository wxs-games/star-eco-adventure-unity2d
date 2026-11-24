Shader "StarEco/Hologram Video"
{
    Properties
    {
        [PerRendererData] _MainTex ("Still Frame", 2D) = "black" {}
        [PerRendererData] _VideoTex ("Hologram Video", 2D) = "black" {}
        [PerRendererData] _VideoBlend ("Show Video", Float) = 0
        [PerRendererData] _VideoUV ("Video UV", Vector) = (1,1,0,0)
        _Color ("Tint", Color) = (1,1,1,1)
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
            #pragma fragment HologramFragment
            #include "UnitySprites.cginc"

            sampler2D _VideoTex;
            float _VideoBlend;
            float4 _VideoUV;

            fixed4 HologramFragment(v2f input) : SV_Target
            {
                // Ava's scene objects were stretched horizontally for the thin legacy sprite.
                float widthScale = length(mul((float3x3)unity_ObjectToWorld, float3(1,0,0)));
                float heightScale = length(mul((float3x3)unity_ObjectToWorld, float3(0,1,0)));
                float fit = (390.0 / 922.0) / (480.0 / 1104.0) * widthScale / max(heightScale, 0.0001);
                float2 uv = float2((input.texcoord.x - 0.5) * fit + 0.5, input.texcoord.y + 0.035);
                float inside = step(0.0, min(uv.x, uv.y)) * step(max(uv.x, uv.y), 1.0);
                uv = saturate(uv);
                fixed3 projected = lerp(SampleSpriteTexture(uv).rgb,
                    tex2D(_VideoTex, uv * _VideoUV.xy + _VideoUV.zw).rgb, _VideoBlend) * inside;
                // The footage is black-backed. Keep low encoding noise out of the scene.
                projected = saturate((projected - 0.012) * 1.012);
                fixed brightness = max(projected.r, max(projected.g, projected.b));
                return fixed4(projected * input.color.rgb * input.color.a,
                    brightness * input.color.a * 0.6);
            }
            ENDCG
        }
    }
}
