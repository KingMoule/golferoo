shader_type spatial;
render_mode cull_back, diffuse_lambert, specular_disabled;

#include "res://shaders/surface_field.gdshaderinc"

// État du gazon, un texel par cellule : R hauteur (0,25 = référence de la surface), G usure.
uniform sampler2D turf_state : filter_linear, repeat_disable;
// Surcouche d'état (touche H) : 1 pour l'afficher.
uniform float overlay = 0.0;
uniform float stripe_width = 6.0;
// Bandes de transition, en mètres : rough tondu autour du départ, du green et du fairway ; lèvre du bunker ; berge de l'eau.
uniform float cut_band = 0.6;
uniform float lip_band = 0.35;
uniform float bank_band = 0.8;
// Rive en pente douce : sous l'eau, le fond s'enfonce au rendu jusqu'à cette profondeur (la sim garde ses hauteurs).
uniform float shore_depth = 0.8;
// Teinte de saison de l'herbe (l'été vaut 1) ; ni le sable, ni l'eau, ni le chemin.
uniform vec3 season_tint = vec3(1.0);
// Sol mouillé : assombrit tout le terrain (0 sec, 1 pluie du jour).
uniform float wetness = 0.0;

varying vec3 wpos;

vec3 srgb(vec3 c) {
	return pow(c, vec3(2.2));
}

// Couleur de base d'une surface, teinte de saison comprise pour l'herbe et le sol des bois.
vec3 base_color(int s) {
	vec3 c;
	if (s == 0) {
		c = vec3(0.45, 0.78, 0.35); // tee
	} else if (s == 1) {
		c = vec3(0.56, 0.90, 0.46); // green
	} else if (s == 2) {
		c = vec3(0.40, 0.715, 0.305); // fairway (rayé plus bas)
	} else if (s == 3) {
		c = vec3(0.24, 0.48, 0.21); // rough
	} else if (s == 4) {
		c = vec3(0.96, 0.91, 0.68); // sable (clair)
	} else if (s == 5) {
		c = vec3(0.20, 0.42, 0.55); // fond d'eau
	} else if (s == 6) {
		c = vec3(0.17, 0.33, 0.15); // sol sous arbres
	} else {
		c = vec3(0.72, 0.64, 0.50); // chemin
	}
	if (s <= 3 || s == 6) {
		c = clamp(c * season_tint, 0.0, 1.0);
	}
	return c;
}

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	// Part d'eau autour du sommet (canal G de B) : passé la rive, le fond descend en pente douce.
	vec2 uv = wpos.xz / grid_size;
	float water = textureLod(weights_b, uv, 0.0).g * 2.0;
	for (int k = 0; k < 8; k++) {
		float ang = float(k) * 0.785398;
		water += textureLod(weights_b, uv + vec2(cos(ang), sin(ang)) * (blur_meters * 0.5) / grid_size, 0.0).g;
	}
	VERTEX.y -= shore_depth * smoothstep(0.55, 0.95, water / 10.0);
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 uv = wpos.xz / grid_size;
	int best;
	int second;
	float dist;
	surface_pick(wpos.xz, best, second, dist);
	vec3 col = base_color(best);
	if (best == 3 && second <= 2 && dist < cut_band) {
		col = mix(base_color(3), base_color(2), 0.45); // rough tondu
	} else if (best == 4 && dist < lip_band) {
		col *= vec3(0.84, 0.81, 0.74); // lèvre du bunker
	} else if (best != 5 && second == 5 && dist < bank_band) {
		col = vec3(0.42, 0.40, 0.27); // berge
	}
	// Anticrénelage : à moins d'un pixel de la frontière, la couleur fond vers la surface voisine.
	float edge = clamp(dist / max(fwidth(dist), 1e-4), 0.0, 1.0);
	col = mix(base_color(second), col, 0.5 + 0.5 * edge);
	if (best <= 3) {
		vec2 st = texture(turf_state, uv).rg;
		float k = st.r * 4.0; // hauteur relative à la référence : 1 = référence
		float wear = st.g;
		// Herbe rase plus claire, haute plus sombre.
		col *= clamp(1.0 - 0.18 * (k - 1.0), 0.6, 1.15);
		// Bandes du fairway : le contraste baisse quand l'herbe a repoussé depuis la tonte.
		float fresh = 1.0 - smoothstep(1.0, 2.0, k);
		float stripe = step(0.5, fract(wpos.x / stripe_width)) - 0.5;
		float checker = mod(floor(wpos.x / 1.5) + floor(wpos.z / 1.5), 2.0) - 0.5;
		if (best == 2) {
			col *= 1.0 + 0.16 * stripe * fresh;
		} else if (best == 1) {
			col *= 1.0 + 0.10 * checker;
		}
		// Terre : petites mottes et marques éparses ; leur nombre suit l'usure (60 % du sol au plus), jamais une tache pleine.
		float n = 0.7 * vnoise(wpos.xz * 4.0) + 0.3 * vnoise(wpos.xz * 11.0);
		float cover = 0.6 * clamp(wear * 1.5, 0.0, 1.0);
		float dirt = smoothstep(1.0 - cover, 1.05 - cover, n);
		col = mix(col, vec3(0.45, 0.33, 0.20), (best == 1 ? 0.45 : 0.7) * dirt);
		if (overlay > 0.5) {
			// Surcouche : vert sain, rouge usé ; jaune pour l'herbe qui a nettement dépassé sa hauteur de référence.
			col = mix(vec3(0.85, 0.12, 0.10), vec3(0.20, 0.70, 0.25), 1.0 - clamp(wear * 2.0, 0.0, 1.0));
			col = mix(col, vec3(0.90, 0.80, 0.20), 0.7 * smoothstep(1.5, 2.5, k));
			// Garder la forme du trou : rough plus sombre, bandes du fairway, damier du green.
			col *= best == 3 ? 0.7 : (best == 2 ? 1.0 + 0.12 * stripe : (best == 1 ? 1.0 + 0.08 * checker : 1.0));
		}
	}
	ALBEDO = srgb(col) * (1.0 - 0.3 * wetness);
	ROUGHNESS = 1.0;
}
