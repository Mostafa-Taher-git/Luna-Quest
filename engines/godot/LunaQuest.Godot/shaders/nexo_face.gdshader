shader_type spatial;
// NEXO's screen face: a dark glossy visor with two curved blue "happy" eyes (^ ^) that blink,
// matching the mission-buddy robot from the AstroNexa cartoon intro.
render_mode unshaded, cull_back;

uniform vec3 eye_color : source_color = vec3(0.35, 0.78, 1.0);
uniform vec3 screen_color : source_color = vec3(0.02, 0.05, 0.09);
uniform float blink = 0.0;      // 0 open .. 1 closed
uniform float mood = 1.0;       // 1 happy arcs, 0 round alert eyes
uniform float alert = 0.0;      // tints the eyes orange for warnings

float arc_eye(vec2 p, vec2 c) {
	vec2 q = p - c;
	q.y /= max(0.08, 1.0 - blink);
	// Happy eye: upper half of a ring.
	float r = length(q);
	float ring = abs(r - 0.085) - 0.022;
	float upper = smoothstep(-0.01, 0.02, q.y);
	float happy = (1.0 - smoothstep(0.0, 0.012, ring)) * upper;
	// Alert eye: a filled rounded dot.
	float dot_eye = 1.0 - smoothstep(0.055, 0.07, r);
	return mix(dot_eye, happy, mood);
}

void fragment() {
	vec2 p = UV;
	p.y = 1.0 - p.y;
	float e = max(arc_eye(p, vec2(0.33, 0.47)), arc_eye(p, vec2(0.67, 0.47)));
	vec3 eye = mix(eye_color, vec3(1.0, 0.55, 0.15), alert);
	// Subtle scanlines and vignette sell the "screen".
	float scan = 0.92 + 0.08 * sin(UV.y * 220.0 + TIME * 6.0);
	float vig = smoothstep(0.75, 0.25, length(UV - vec2(0.5)));
	vec3 col = screen_color * (0.6 + 0.4 * vig) + eye * e * 2.4 * scan;
	ALBEDO = col;
}
