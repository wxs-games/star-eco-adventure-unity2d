Shader "StarEco/Level Background Video"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _VideoTex ("Background Video", 2D) = "black" {}
        [PerRendererData] _VideoBlend ("Show Video", Float) = 0
        [PerRendererData] _VideoUV ("Video UV", Vector) = (1,1,0,0)
        _Color ("Tint", Color) = (1,1,1,1)
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
            #pragma fragment VideoFragment
            #include "UnitySprites.cginc"
            sampler2D _VideoTex;
            float _VideoBlend;
            float4 _VideoUV;

            fixed4 VideoFragment(v2f input) : SV_Target
            {
                fixed4 color = lerp(SampleSpriteTexture(input.texcoord),
                    tex2D(_VideoTex, input.texcoord * _VideoUV.xy + _VideoUV.zw), _VideoBlend) * input.color;
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
