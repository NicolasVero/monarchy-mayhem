using System.Collections.Generic;
using UnityEngine;

// Gerbe d'impact.
//
// Le projet n'a aucun VFX de combat réutilisable : pas de sang, pas d'étincelle, pas
// de trail. Les seules particules existantes sont celles de l'attaque ennemie. Plutôt
// que de dépendre d'un prefab tiers au chemin fragile, la gerbe est construite en
// code — autonome, sans asset à importer, et réglable ici.
public class ImpactVfx : MonoBehaviour {

    private const int POOL_SIZE = 12;

    private readonly List<ParticleSystem> pool = new List<ParticleSystem>(POOL_SIZE);
    private int next;

    public void Spawn(Vector3 position, Vector3 normal, Color color, float scale) {

        ParticleSystem system = this.Take();
        if(system == null) return;

        system.transform.position = position;
        system.transform.rotation = (normal.sqrMagnitude > 0.0001f)
                                  ? Quaternion.LookRotation(normal)
                                  : Quaternion.identity;

        ParticleSystem.MainModule main = system.main;
        main.startColor = color;
        main.startSizeMultiplier = 0.09f * scale;
        main.startSpeedMultiplier = 5f * scale;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.SetBurst(0, new ParticleSystem.Burst(0f, (short) Mathf.RoundToInt(8 * scale), (short) Mathf.RoundToInt(14 * scale)));

        system.Clear();
        system.Play();
    }

    private ParticleSystem Take() {

        if(this.pool.Count < POOL_SIZE) {
            ParticleSystem created = this.Build();
            this.pool.Add(created);

            return created;
        }

        // Tourniquet : au-delà du pool, on recycle le plus ancien.
        ParticleSystem system = this.pool[this.next];
        this.next = (this.next + 1) % this.pool.Count;

        return system;
    }

    private ParticleSystem Build() {

        GameObject go = new GameObject("ImpactVfx");
        go.transform.SetParent(transform, false);

        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop();

        ParticleSystem.MainModule main = system.main;
        main.duration = 0.4f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 0.25f;
        main.startSize = 0.09f;
        main.startSpeed = 5f;
        main.gravityModifier = 1.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 32;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 8, 14) });

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.05f;

        // Les particules s'éteignent en rétrécissant : lisible même en horde.
        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.12f;
        renderer.material = BuildMaterial();

        return system;
    }

    private static Material BuildMaterial() {

        // Shader additif toujours disponible en Built-in : le projet n'utilise
        // ni URP ni HDRP.
        Shader shader = Shader.Find("Particles/Standard Unlit");
        if(shader == null) shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader);
        material.SetFloat("_Mode", 2f);

        return material;
    }
}
