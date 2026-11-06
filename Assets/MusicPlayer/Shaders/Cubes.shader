Shader "Custom/Cubes"
{
    Properties
    {
        [MainColor] bgColor("Background Color", Color) = (1, 1, 1, 1)
        rectColor("rectangle Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float3 bgColor = float3(0.01, 0.16, 0.42);
            float3 rectColor = float3(0.01, 0.26, 0.57);

            //noise background
            const float noiseIntensity = 2.8;
            const float noiseDefinition = 0.6;
            const float2 glowPos = float2(-2., 0.);

            //rectangles
            const float total = 60.;//number of rectangles
            const float minSize = 0.03;//rectangle min size
            const float maxSize = 0.08;//rectangle max size
            const float yDistribution = 0.5;


            float random(float2 co){
                return frac(sin(dot(co.xy ,float2(12.9898,78.233))) * 43758.5453);
            }

            float noise( in float2 p )
            {
                p*=noiseIntensity;
                float2 i = floor( p );
                float2 f = frac( p );
	            float2 u = f*f*(3.0-2.0*f);
                return lerp( lerp( random( i + float2(0.0,0.0) ), 
                                 random( i + float2(1.0,0.0) ), u.x),
                            lerp( random( i + float2(0.0,1.0) ), 
                                 random( i + float2(1.0,1.0) ), u.x), u.y);
            }

            float fbm( in float2 uv )
            {	
	            uv *= 5.0;
                float2x2 m = float2x2( 1.6,  1.2, -1.2,  1.6 );
                float f  = 0.5000*noise( uv ); uv = mul(m, uv);
                f += 0.2500*noise( uv ); uv = mul(m, uv);
                f += 0.1250*noise( uv ); uv = mul(m, uv);
                f += 0.0625*noise( uv ); uv = mul(m, uv);
    
	            f = 0.5 + 0.5*f;
                return f;
            }

            float3 bg(float2 uv )
            {
                float velocity = _Time.y/1.6;
                float intensity = sin(uv.x*3.+velocity*2.)*1.1+1.5;
                uv.y -= 2.;
                float2 bp = uv+glowPos;
                uv *= noiseDefinition;

                //ripple
                float rb = fbm(float2(uv.x*.5-velocity*.03, uv.y))*.1;
                //rb = sqrt(rb); 
                uv += rb;

                //coloring
                float rz = fbm(uv*.9+float2(velocity*.35, 0.0));
                rz *= dot(bp*intensity,bp)+1.2;

                //bazooca line
                //rz *= sin(uv.x*.5+velocity*.8);


                float3 col = bgColor/(.1-rz);
                return sqrt(abs(col));
            }


            float rectangle(float2 uv, float2 pos, float width, float height, float blur) {
    
                pos = (float2(width, height) + .01)/2. - abs(uv - pos);
                pos = smoothstep(0., blur , pos);
                return pos.x * pos.y; 
   
            }

            float2x2 rotate2d(float _angle){
                return float2x2(cos(_angle),-sin(_angle),
                            sin(_angle),cos(_angle));
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.positionHCS.xy / _ScreenParams.xy * 2. - 1.;
                uv.x *= _ScreenParams.x / _ScreenParams.y;

                float3 color = bg(uv) * (2. - abs(uv.y * 2.));

                float velX = -_Time.y / 8.;
                float velY = _Time.y / 10.;

                for (float i = 0.; i < total; i++)
                {
                    float index = i / total;
                    float rnd = random(index);
                    float3 pos = float3(0, 0., 0.);

                    pos.x = frac(velX * rnd + index) * 4. - 2.0;
                    pos.y = sin(index * rnd * 1000. + velY) * yDistribution;
                    pos.z = maxSize * rnd + minSize;

                    float2 uvRot = uv - pos.xy + pos.z / 2.;
                    uvRot = mul(rotate2d(i + _Time.y / 2.), uvRot);
                    uvRot += pos.xy + pos.z / 2.;

                    float rect = rectangle(uvRot, pos.xy, pos.z, pos.z, (maxSize + minSize - pos.z) / 2.);
                    color += rectColor * rect * pos.z / maxSize;
                }

                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
