Shader "StarEco/Player3DSpriteLit"
{
    Properties
    {
        _MainTex ("Base color", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 _Color;
            struct Vertex { float4 position : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct Pixel { float4 position : SV_POSITION; float3 normal : TEXCOORD0; float2 uv : TEXCOORD1; };
            Pixel vert(Vertex input)
            {
                Pixel output;
                output.position = UnityObjectToClipPos(input.position);
                output.normal = UnityObjectToWorldNormal(input.normal);
                output.uv = input.uv;
                return output;
            }
            fixed4 frag(Pixel input) : SV_Target
            {
                clip(_Color.a - .01);
                fixed3 color = tex2D(_MainTex, input.uv).rgb * _Color.rgb;
                float light = .68 + .32 * saturate(dot(normalize(input.normal), normalize(float3(-.4,.8,-1))));
                return fixed4(color * light, _Color.a);
            }
            ENDCG
        }
    }
}
