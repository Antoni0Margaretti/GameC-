Shader "Custom/DepixelTransition" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
        _Pixelation ("Pixelation", Range(1, 100)) = 100
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        LOD 100

        Pass {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Pixelation;
            float _Fade;
            
            // Основной фрагментный шейдер
            fixed4 frag(v2f_img i) : SV_Target {
                // Исходные UV
                float2 uv = i.uv;
                // Применяем эффект пикселизации: умножаем UV на _Pixelation, округляем и делим обратно.
                float2 scaledUV = floor(uv * _Pixelation) / _Pixelation;
                
                // Читаем цвет текстуры по видоизмененным координатам
                fixed4 col = tex2D(_MainTex, scaledUV);
                
                // Применяем затемнение: при _Fade = 1 цвет смешивается с черным
                col.rgb = lerp(col.rgb, 0, _Fade);
                
                return col;
            }
            ENDCG
        }
    }
}