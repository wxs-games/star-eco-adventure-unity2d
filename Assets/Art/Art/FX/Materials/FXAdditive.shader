// FX 打击特效专用加法混合 Sprite Shader（内置管线）。
// 用途：fx_slash / fx_hit / fx_purify / fx_explode —— 黑底完全透明，自然发光。
//
// 为什么不能直接用 Legacy Shaders/Particles/Additive：
//   它会做 2.0 倍增亮，且与 SpriteRenderer 的顶点色/翻转兼容性不如 Sprites 系。
// 为什么必须预乘 alpha（c.rgb *= c.a）：
//   实测本项目 6 套 FX 帧的**透明区 RGB 最大值为 22**（不是纯黑 0）。
//   纯 `Blend One One` 下透明区会贡献最多 22/255 的暗色 → 画面出现方形色块。
//   预乘后 alpha=0 处贡献恒为 0，边缘渐隐也自然。
//
// 样式与项目既有 Assets/Resources/Shaders/SpriteFlash.shader 保持一致（同一套写法）。
Shader "StarEco/FXAdditive"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Additive Intensity", Range(0,4)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Intensity;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;
                c.rgb *= c.a;            // 预乘 alpha：透明区不贡献亮度
                c.rgb *= _Intensity;    // 亮度调节（打击反馈强弱）
                c.a = 0;
                return c;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
