shader_type spatial;

// Arbres : couleur unie (ou couleur de sommet) et couronnes tramées autour des points focaux.
// Chaque point focal est (x, y, z, rayon en mètres), en coordonnées monde ; un rayon nul le désactive.
// Les fragments plus proches de la caméra que le point focal sont jetés en tramage (Bayer 4 x 4) dans le rayon,
// à l'écran : le golfeur ou la balle se lit à travers la couronne qui le cache.
global uniform vec4 tree_focus_a;
global uniform vec4 tree_focus_b;

uniform vec4 albedo : source_color = vec4(0.3, 0.5, 0.3, 1.0);
uniform bool use_vertex_color = false;
uniform float roughness_value = 1.0;

// Un fragment à moins de cette distance (mètres) de la profondeur du point focal reste opaque.
const float FRONT_MARGIN = 0.5;

float bayer4(vec2 p) {
	int x = int(mod(p.x, 4.0));
	int y = int(mod(p.y, 4.0));
	const float m[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	return (m[(y * 4) + x] + 0.5) / 16.0;
}

// 0 hors du rayon ou derrière le point focal, jusqu'à 1 au centre. Caméra orthographique : proj[1][1] = 2 / hauteur de vue en mètres.
float focus_fade(vec4 focus, vec3 frag_view, vec2 uv, mat4 view, mat4 proj, vec2 viewport) {
	if (focus.w <= 0.0) {
		return 0.0;
	}
	vec3 fv = (view * vec4(focus.xyz, 1.0)).xyz;
	if (frag_view.z < fv.z + FRONT_MARGIN) {
		return 0.0;
	}
	vec4 c = proj * vec4(fv, 1.0);
	vec2 fuv = (c.xy / c.w) * 0.5 + 0.5;
	vec2 d = (uv - fuv) * vec2(viewport.x / viewport.y, 1.0);
	float r = focus.w * proj[1][1] * 0.5;
	return 1.0 - smoothstep(r * 0.55, r, length(d));
}

void fragment() {
	float fade = max(
		focus_fade(tree_focus_a, VERTEX, SCREEN_UV, VIEW_MATRIX, PROJECTION_MATRIX, VIEWPORT_SIZE),
		focus_fade(tree_focus_b, VERTEX, SCREEN_UV, VIEW_MATRIX, PROJECTION_MATRIX, VIEWPORT_SIZE));
	if (fade > bayer4(FRAGCOORD.xy)) {
		discard;
	}
	vec3 base = albedo.rgb;
	if (use_vertex_color) {
		base *= COLOR.rgb;
	}
	ALBEDO = base;
	ROUGHNESS = roughness_value;
}
