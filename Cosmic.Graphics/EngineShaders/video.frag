/*
Channel conversion shader adapted from FNA (https://github.com/FNA-XNA/FNA)

Microsoft Public License (Ms-PL)
FNA - Copyright 2009-2023 Ethan Lee and the MonoGame Team

All rights reserved.

This license governs use of the accompanying software. If you use the software,
you accept this license. If you do not accept the license, do not use the
software.

1. Definitions

The terms "reproduce," "reproduction," "derivative works," and "distribution"
have the same meaning here as under U.S. copyright law.

A "contribution" is the original software, or any additions or changes to the
software.

A "contributor" is any person that distributes its contribution under this
license.

"Licensed patents" are a contributor's patent claims that read directly on its
contribution.

2. Grant of Rights

(A) Copyright Grant- Subject to the terms of this license, including the
license conditions and limitations in section 3, each contributor grants you a
non-exclusive, worldwide, royalty-free copyright license to reproduce its
contribution, prepare derivative works of its contribution, and distribute its
contribution or any derivative works that you create.

(B) Patent Grant- Subject to the terms of this license, including the license
conditions and limitations in section 3, each contributor grants you a
non-exclusive, worldwide, royalty-free license under its licensed patents to
make, have made, use, sell, offer for sale, import, and/or otherwise dispose of
its contribution in the software or derivative works of the contribution in the
software.

3. Conditions and Limitations

(A) No Trademark License- This license does not grant you rights to use any
contributors' name, logo, or trademarks.

(B) If you bring a patent claim against any contributor over patents that you
claim are infringed by the software, your patent license from such contributor
to the software ends automatically.

(C) If you distribute any portion of the software, you must retain all
copyright, patent, trademark, and attribution notices that are present in the
software.

(D) If you distribute any portion of the software in source code form, you may
do so only under this license by including a complete copy of this license with
your distribution. If you distribute any portion of the software in compiled or
object code form, you may only do so under a license that complies with this
license.

(E) The software is licensed "as-is." You bear the risk of using it. The
contributors give no express warranties, guarantees or conditions. You may have
additional consumer rights under your local laws which this license cannot
change. To the extent permitted under your local laws, the contributors exclude
the implied warranties of merchantability, fitness for a particular purpose and
non-infringement.
*/

layout(set = 0, binding = 0) uniform texture2D VideoY;
layout(set = 0, binding = 1) uniform texture2D VideoU;
layout(set = 0, binding = 2) uniform texture2D VideoV;
layout(set = 0, binding = 3) uniform sampler Sampler;

layout(location = 0) in vec2 fsin_uv;
layout(location = 0) out vec4 fsout_color;

void main()
{
    const vec3 offset = vec3(-0.0625, -0.5, -0.5);

    /* More info about colorspace conversion:
        * http://www.equasys.de/colorconversion.html
        * http://www.equasys.de/colorformat.html
        */
#if 1
    /* ITU-R BT.709 */
    const vec3 Rcoeff = vec3(1.164,  0.000,  1.793);
    const vec3 Gcoeff = vec3(1.164, -0.213, -0.533);
    const vec3 Bcoeff = vec3(1.164,  2.112,  0.000);
#else
    /* ITU-R BT.601 */
    const vec3 Rcoeff = vec3(1.164, 0.000, 1.596);
    const vec3 Gcoeff = vec3(1.164, -0.391, -0.813);
    const vec3 Bcoeff = vec3(1.164, 2.018, 0.000);
#endif

    vec3 yuv;
    yuv.x = texture(sampler2D(VideoY, Sampler), fsin_uv).r;
    yuv.y = texture(sampler2D(VideoU, Sampler), fsin_uv).r;
    yuv.z = texture(sampler2D(VideoV, Sampler), fsin_uv).r;
    yuv += offset;

    vec4 rgba;
    rgba.x = dot(yuv, Rcoeff);
    rgba.y = dot(yuv, Gcoeff);
    rgba.z = dot(yuv, Bcoeff);
    rgba.w = 1.0;
    fsout_color = rgba;
}