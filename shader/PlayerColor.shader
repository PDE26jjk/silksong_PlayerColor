// Place it in a Unity 6000.0.50f1 project and package it as an AssetBundle named ssmod
Shader "Mod/PlayerColor"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _MaskFromColor1 ("_MaskFromColor1", Color) = (1,1,1,1)
        _MaskColor1 ("_MaskColor1", Color) = (1,1,1,1)
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _FlashAmount ("Flash Amount", Range(0, 1)) = 0
        _HueShift ("Hue Shift", Range(0, 1)) = 1
        _BlackThreadAmount ("Black Thread Amount", Range(0, 2)) = 1
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [MaterialToggle] CAN_HUESHIFT ("Enable Hue Shift", Float) = 0
        [MaterialToggle] BLACKTHREAD ("Enable Black Thread", Float) = 0
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
        Fog
        {
            Mode Off
        }
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON
            // #pragma multi_compile __ BLACKTHREAD
            // #pragma multi_compile __ CAN_HUESHIFT
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                half2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _MaskTex;
            fixed4 _Color;
            fixed4 _MaskFromColor1;
            fixed4 _MaskColor1;
            fixed4 _FlashColor;
            float _FlashAmount;
            float t1;
            float t2;

            v2f vert(appdata_t IN) {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;

                OUT.color = IN.color * _Color;
                #ifdef PIXELSNAP_ON
					OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target {
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;
                fixed mask = tex2D(_MaskTex, IN.texcoord).r * 255;
                fixed _t1 = 0.75;
                fixed _t2 = 1.6;
                if (mask > 0) {
                    fixed gray = dot(c / _MaskFromColor1, fixed3(0.22, 0.707, 0.071));
                    c = lerp(c, _MaskColor1, gray);
                    _t1 = t1;
                    _t2 = t2;
                }

                fixed3 blackThread = c.rgb * _FlashColor.rgb * _t1;
                c.rgb = blackThread * 0.4 + c.rgb * 0.6;

                fixed gray = dot(c.rgb, fixed3(0.22, 0.707, 0.071));
                fixed3 grayColor = gray;
                c.rgb = lerp(grayColor, c.rgb, _t2);

                c.a *= IN.color.a;
                c.rgb = lerp(c.rgb, _FlashColor.rgb, _FlashAmount);
                c.rgb *= c.a;

                return c;
            }
            ENDCG
        }
    }
}