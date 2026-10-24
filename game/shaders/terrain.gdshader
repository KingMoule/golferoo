shader_type spatial;
render_mode cull_back, diffuse_lambert, specular_disabled;

// Deux textures RGBA d'un texel par cellule : poids "un parmi huit" par surface.
// Le filtrage bilinéaire adoucit les bords, le bruit les rend courbes, puis on prend le poids maximal.
uniform sampler2D weights_a : filter_linear, repeat_disable;
uniform sampler2D weights_b : filter_linear, repeat_disable;
uniform vec2 grid_size = vec2(384.0, 128.0);
uniform float noise_amount = 0.45;
uniform float noise_scale = 0.35;
uniform float stripe_width = 6.0;
// Rayon du flou des poids, en mètres. Le trou est dessiné par tuiles de 8 m : sans flou plus large
// qu'une cellule, les bords gardent l'escalier des tuiles.
uniform float blur_meters = 5.0;

varying vec3 wpos;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x),
			mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

vec3 srgb(vec3 c) {
	return pow(c, vec3(2.2));
}

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 uv = wpos.xz / grid_size;
	// Flou en deux couronnes (8 + 8 prises) autour du centre.
	vec4 a = texture(weights_a, uv) * 2.0;
	vec4 b = texture(weights_b, uv) * 2.0;
	float total = 2.0;
	for (int k = 0; k < 8; k++) {
		float ang = float(k) * 0.785398;
		vec2 dir = vec2(cos(ang), sin(ang));
		vec2 o1 = dir * (blur_meters * 0.5) / grid_size;
		vec2 o2 = vec2(cos(ang + 0.392699), sin(ang + 0.392699)) * blur_meters / grid_size;
		a += texture(weights_a, uv + o1) + 0.6 * texture(weights_a, uv + o2);
		b += texture(weights_b, uv + o1) + 0.6 * texture(weights_b, uv + o2);
		total += 1.6;
	}
	a /= total;
	b /= total;
	float w[8];
	w[0] = a.r; w[1] = a.g; w[2] = a.b; w[3] = a.a;
	w[4] = b.r; w[5] = b.g; w[6] = b.b; w[7] = b.a;

	int best = 0;
	float best_v = -1.0;
	for (int i = 0; i < 8; i++) {
		float v = w[i] + (vnoise(wpos.xz * noise_scale + vec2(float(i) * 17.3, float(i) * 5.1)) - 0.5) * noise_amount;
		if (v > best_v) {
			best_v = v;
			best = i;
		}
	}

	vec3 col;
	if (best == 0) {
		col = vec3(0.45, 0.78, 0.35); // tee
	} else if (best == 1) {
		col = vec3(0.56, 0.90, 0.46); // green
	} else if (best == 2) {
		float s = step(0.5, fract(wpos.x / stripe_width));
		col = mix(vec3(0.38, 0.69, 0.29), vec3(0.42, 0.74, 0.32), s); // fairway rayé
	} else if (best == 3) {
		col = vec3(0.24, 0.48, 0.21); // rough
	} else if (best == 4) {
		col = vec3(0.89, 0.82, 0.55); // sable
	} else if (best == 5) {
		col = vec3(0.20, 0.42, 0.55); // fond d'eau
	} else if (best == 6) {
		col = vec3(0.17, 0.33, 0.15); // sol sous arbres
	} else {
		col = vec3(0.72, 0.64, 0.50); // chemin
	}
	ALBEDO = srgb(col);
	ROUGHNESS = 1.0;
}
