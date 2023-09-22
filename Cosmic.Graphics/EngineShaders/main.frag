layout(set = 0, binding = 0) uniform texture2D Input;
layout(set = 0, binding = 1) uniform sampler Sampler;

layout(location = 0) in vec2 fsin_uv;
layout(location = 0) out vec4 fsout_color;

void main()
{
    fsout_color = texture(sampler2D(Input, Sampler), fsin_uv);
}