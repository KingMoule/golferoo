shader_type spatial;
render_mode depth_draw_opaque, cull_back, specular_schlick_ggx;

#include "res://shaders/surface_field.gdshaderinc"

uniform vec3 shallow_color : source_color = vec3(0.25, 0.60, 0.75);
uniform vec3 deep_color : source_color = vec3(0.12, 0.38, 0.58);
// Plans d'eau : le maillage déborde d'une cellule et le champ des surfaces trace la rive (la même que celle du terrain) ;
// l'eau s'éclaircit sur ses premiers mètres, comme sur une rive en pente douce. Sans masque, le carré entier.
uniform bool shore_mask = false;
uniform float shallow_meters = 1.5;

varying vec3 wpos;

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	VERTEX.y += sin(wpos.x * 0.7 + TIME * 0.8) * cos(wpos.z * 0.6 + TIME * 0.6) * 0.04;
}

void fragment() {
	// Une seule surface lisse et opaque : ni vagues en couleur (la somme de deux sinus obliques dessine un damier de losanges)
	// ni transparence (le fond du terrain, enfoncé, se voyait au travers comme une seconde couche et une bande sombre en bordure).
	float inside = 1.0;
	if (shore_mask) {
		int best;
		int second;
		float dist;
		surface_pick(wpos.xz, best, second, dist);
		inside = best == 5 ? dist : -dist;
		if (inside < 0.0) {
			discard;
		}
	}
	ALBEDO = mix(shallow_color, deep_color, smoothstep(0.0, shallow_meters * 2.0, inside));
	ROUGHNESS = 0.15;
}
