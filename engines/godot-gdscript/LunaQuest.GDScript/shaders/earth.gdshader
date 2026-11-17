shader_type spatial;
// Earth seen from the lunar south pole: a NASA Blue Marble surface, a lit day side facing the
// Sun with a soft terminator, a faint night side, and a thin blue atmospheric limb. The sun_dir
// is fed from the same code path as the colony's directional sun, so the Earth always faces the
// phase the colony sees.
render_mode unshaded, cull_back;
uniform sampler2D surface : source_color, filter_linear_mipmap;
uniform vec3 sun_dir = vec3(0.4, 0.3, 0.85);   // world-space direction towards the sun
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
    // Lit side: full surface, night side almost dark (Earth is not a lamp).
    vec3 lit = col * brightness * (0.06 + 0.94 * day);
    // A thin blue atmospheric crescent hugs the lit limb only.
    float limb = fres * smoothstep(0.0, 0.35, ndl);
    lit = mix(lit, atmosphere * (0.20 + 0.85 * day), limb * 0.8);
    ALBEDO = lit;
}
