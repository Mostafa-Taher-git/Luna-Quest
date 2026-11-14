shader_type spatial;
// Earth seen from the lunar south pole: NASA Blue Marble surface, a lit day side facing the
// Sun with a soft terminator, a faint night side, and a thin blue atmospheric limb.
render_mode unshaded, cull_back;
uniform sampler2D surface : source_color, filter_linear_mipmap;
uniform vec3 sun_dir = vec3(0.4, 0.3, 0.85);   // world-space direction towards the Sun
uniform vec3 atmosphere : source_color = vec3(0.36, 0.62, 1.0);
uniform float brightness = 1.15;
varying vec3 wn;
void vertex() {
    wn = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}
void fragment() {
    vec3 col = texture(surface, UV).rgb;
    float ndl = dot(normalize(wn), normalize(sun_dir));
    float day = smoothstep(-0.12, 0.25, ndl);
    vec3 v = normalize(-VERTEX);
    float fres = pow(1.0 - clamp(dot(NORMAL, v), 0.0, 1.0), 3.0);
    vec3 lit = col * brightness * (0.08 + 0.92 * day);
    lit = mix(lit, atmosphere * (0.25 + 0.9 * day), fres * 0.85);
    ALBEDO = lit;
}
