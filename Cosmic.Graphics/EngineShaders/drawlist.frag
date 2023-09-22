layout(set = 0, binding = 0) uniform texture2D LastFramebuffer;
layout(set = 0, binding = 1) uniform texture3D Surfaces;
layout(set = 0, binding = 2) uniform texture2D Palettes;
layout(set = 0, binding = 3) uniform texture1D PaletteLines;
layout(set = 0, binding = 4) uniform texture3D Chunks;
layout(set = 0, binding = 5) uniform texture2D Floor3d;
layout(set = 0, binding = 6) uniform sampler Sampler;
layout(set = 0, binding = 7) uniform Vars
{
    int blendMode;
    int screenScale;
    int floor3Dx;
    int floor3Dy;
    int floor3Dz;
    int floor3Dangle;
} vars;

layout(location = 0) in vec3 fsin_pos;
layout(location = 1) in vec3 fsin_uv;
layout(location = 2) in vec4 fsin_color;
layout(location = 0) out vec4 fsout_color;

mat2 rotation2d(float angle) {
  float s = sin(angle);
  float c = cos(angle);

  return mat2(
    c, -s,
    s, c
  );
}

void main()
{
    float colorNum = 0;
    vec4 sourceColor = vec4(0);
    switch (vars.blendMode)
    {
        case BLEND_STRAIGHTCOLOR: // straight color
            fsout_color = fsin_color.bgra;
            break;
        case BLEND_PAUSEMENU: // tinted 4-point blur
            vec2 blurRadius = (1.0 / vec2(SCREEN_XSIZE, SCREEN_YSIZE)) * (0.25 * vars.screenScale);
            vec3 topleft = texture(sampler2D(LastFramebuffer, Sampler), clamp(fsin_pos.xy + vec2(blurRadius.x, blurRadius.y), vec2(0), vec2(1) - blurRadius)).rgb;
            vec3 topright = texture(sampler2D(LastFramebuffer, Sampler), clamp(fsin_pos.xy + vec2(-blurRadius.x, blurRadius.y), vec2(0), vec2(1) - blurRadius)).rgb;
            vec3 bottomleft = texture(sampler2D(LastFramebuffer, Sampler), clamp(fsin_pos.xy + vec2(blurRadius.x, -blurRadius.y), vec2(0), vec2(1) - blurRadius)).rgb;
            vec3 bottomright = texture(sampler2D(LastFramebuffer, Sampler), clamp(fsin_pos.xy + vec2(-blurRadius.x, -blurRadius.y), vec2(0), vec2(1) - blurRadius)).rgb;
            vec3 finalColor = mix(
                mix(topleft, topright, 0.5),
                mix(bottomleft, bottomright, 0.5),
                0.5
            );
            float finalColorAverage = (finalColor.r + finalColor.g + finalColor.b) / 3;
            fsout_color = vec4(
                mix(vec3(min(finalColorAverage  + (3 / 16.0), 1)),
                vec3(224.0 / 255.0, 156.0 / 255.0, 40.0 / 255.0), 0.25),
            1);
            break;
        case BLEND_ALPHA:
        case BLEND_ADDITIVE:
        case BLEND_SUBTRACTIVE:
        case BLEND_OPAQUE:
            colorNum = texture(sampler3D(Surfaces, Sampler), fsin_uv / vec3(MaxHwTextureDimension, MaxHwTextureDimension, MaxHwTextures)).r;
            if (colorNum == 0)
            {
                discard;
                return;
            }
            fsout_color = texture(
                sampler2D(Palettes, Sampler),
                vec2(
                    colorNum,
                    texture(sampler1D(PaletteLines, Sampler), fsin_pos.y).r * 32
                )
            ) * fsin_color;
            break;
        case BLEND_TILESET:
            colorNum = texture(sampler3D(Chunks, Sampler), fsin_uv).r;
            if (colorNum == 0)
            {
                discard;
                return;
            }
            fsout_color = texture(
                sampler2D(Palettes, Sampler),
                vec2(
                    colorNum,
                    texture(sampler1D(PaletteLines, Sampler), fsin_pos.y).r * 32
                )
            );
            break;
        case BLEND_FLOOR3D:
            int layerWidth         = 4096;
            int layerHeight        = 4096;
            int layerYPos          = int(vars.floor3Dy);
            int layerZPos          = int(vars.floor3Dz);
            int sinValue           = int(sin((vars.floor3Dangle / 256.0) * 3.14159) * 4096.0);
            int cosValue           = int(cos((vars.floor3Dangle / 256.0) * 3.14159) * 4096.0);
            int layerXPos          = int(vars.floor3Dx) >> 4;
            int ZBuffer            = layerZPos >> 4;

            float i = fsin_uv.y * (SCREEN_YSIZE / 2 - 12);

            float XBuffer    = layerYPos / (i * 512.0) * (-cosValue / 256.0);
            float YBuffer    = sinValue * ((layerYPos / (i * 512.0)) / 256.0);
            float XPos       = layerXPos + (3 * sinValue * (layerYPos / (i * 512.0)) / 4) - XBuffer * SCREEN_CENTERX;
            float YPos       = ZBuffer + (3 * cosValue * (layerYPos / (i * 512.0)) / 4) - YBuffer * SCREEN_CENTERX;

            float tileX = (XPos + XBuffer * (fsin_uv.x * SCREEN_XSIZE)) / 4096.0;
            float tileY = (YPos + YBuffer * (fsin_uv.x * SCREEN_XSIZE)) / 4096.0;

            if (tileX > -1 && tileX < layerWidth && tileY > -1 && tileY < layerHeight) {
                int tileInfo = int(texture(sampler2D(Floor3d, Sampler), vec2(tileX / 4096.0, tileY / 4096.0)).r * 65535.0);
                float tileIndex = float(tileInfo & 0x3ff) / 1024.0;
                float tilePX = floor(mod(tileX, 16.0));
                float tilePY = floor(mod(tileY, 16.0));
                if ((tileInfo & 0x400) != 0) {
                    tilePX = 15.0 - tilePX;
                }
                if ((tileInfo & 0x800) != 0) {
                    tilePY = 15.0 - tilePY;
                }

                colorNum = texture(sampler3D(Chunks, Sampler), vec3(tilePX / 16.0, tilePY / 16.0, tileIndex)).r;
                if (colorNum == 0)
                {
                    discard;
                    return;
                }
                fsout_color = texture(
                    sampler2D(Palettes, Sampler),
                    vec2(
                        colorNum,
                        texture(sampler1D(PaletteLines, Sampler), fsin_pos.y).r * 32
                    )
                );
            } else {
                discard;
                return;
            }
            break;
        case BLEND_TILESETDEBUG:
            if (texture(sampler3D(Chunks, Sampler), fsin_uv).r == 0)
            {
                discard;
                return;
            }
            fsout_color = fsin_color.bgra;
            break;
        case BLEND_MONOCHROME:
            colorNum = texture(sampler3D(Surfaces, Sampler), fsin_uv / vec3(MaxHwTextureDimension, MaxHwTextureDimension, MaxHwTextures)).r;
            if (colorNum == 0)
            {
                discard;
                return;
            }
            sourceColor = texture(
                sampler2D(Palettes, Sampler),
                vec2(
                    colorNum,
                    texture(sampler1D(PaletteLines, Sampler), fsin_pos.y).r * 32
                )
            );
            fsout_color = vec4(
                vec3(
                    min(
                        (
                            (
                                sourceColor.r +
                                sourceColor.g +
                                sourceColor.b
                            ) / 3
                        ) + (3 / 16.0),
                        1
                    )
                ),
            1);
            break;
        default:
            fsout_color = vec4(fsin_uv.x, fsin_uv.y, fsin_uv.x, 1.0);
            break;
    }
}