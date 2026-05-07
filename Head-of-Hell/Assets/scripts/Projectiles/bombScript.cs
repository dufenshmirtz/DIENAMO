using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Collections;
using UnityEngine;

public class bombScript : MonoBehaviour
{
    public Animator animator;
    int player;
    bool exploded = false;
    bool dmgEnd = false;
    bool damageDealt = false;
    Character playa;
    Steelager steelager;
    AudioManager audioManager;
    bool jumpDone = false;

    // Start is called before the first frame update
    void Start()
    {
        player = 0;
        audioManager = FindObjectOfType<AudioManager>(); // Find and assign the AudioManager

        if (audioManager == null)
        {
            Debug.LogError("AudioManager not found in the scene!");
        }
    }

    // Update is called once per frame
    void Update()
    {

    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if ((player == 0))
        {
            if (other.CompareTag("Player"))
            {
                playa = other.GetComponent<Character>();
                if (playa == null)
                {
                    playa = other.GetComponentInParent<Character>();
                }
                if (playa != null)
                {
                    player = playa.PlayerId == "P1" ? 1 : playa.PlayerId == "P2" ? 2 : 3;
                }
            }

            return;
        }

        Character character = other.GetComponent<Character>();
        if (character == null)
        {
            character = other.GetComponentInParent<Character>();
        }
        if (character == null)
        {
            return;
        }

        bool isOwner = character == playa;
        bool validEnemy = !isOwner;

        if (isOwner && exploded && !dmgEnd && !jumpDone)
        {
            // Steelager's passive: his own bomb launches him toward the closest opponent.
            Character closestEnemy = FindClosestEnemyForOwner();
            if (closestEnemy != null)
            {
                character.SetEnemy(closestEnemy);
            }
            character.Knockback(13f, 0.3333f, true);
            jumpDone = true;
            return;
        }

        if (validEnemy && exploded && !dmgEnd && !damageDealt)
        {
            TelemetryManager.Instance?.LogHitAttempt(playa.PlayerId, character.PlayerId, MoveType.Projectile);
            character.SetIncomingDamageContext(playa.PlayerId, MoveType.Projectile, SourceType.Projectile);

            character.TakeDamage(6, true);
            damageDealt = true;
        }

        if (validEnemy && !exploded && !dmgEnd && !damageDealt)
        {
            Explode();

            TelemetryManager.Instance?.LogHitAttempt(playa.PlayerId, character.PlayerId, MoveType.Projectile);
            character.SetIncomingDamageContext(playa.PlayerId, MoveType.Projectile, SourceType.Projectile);

            character.TakeDamage(6, true);
            damageDealt = true;
        }

    }

    private Character FindClosestEnemyForOwner()
    {
        if (playa == null)
        {
            return null;
        }

        Character[] characters = FindObjectsOfType<Character>();
        Character closest = null;
        float closestDistance = float.MaxValue;

        foreach (Character candidate in characters)
        {
            if (candidate == null || candidate == playa || !candidate.isActiveAndEnabled || candidate.IsDead())
            {
                continue;
            }

            float distance = Vector2.Distance(playa.transform.position, candidate.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = candidate;
            }
        }

        return closest;
    }

    public void Explode()
    {
        if (!exploded)
        {
            exploded = true;
            Collider2D[] colliders = GetComponents<Collider2D>();
            colliders[2].enabled = true;
            if (player != 0)
            {
                audioManager.BoomSound();
            }
            animator.SetTrigger("Explode");

        }
    }

    public void DestroyBomb()
    {
        Destroy(gameObject);
    }

    public void EndExplosion()
    {
        dmgEnd = true;
    }
}
