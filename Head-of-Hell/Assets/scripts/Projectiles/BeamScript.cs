using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BeamScript : MonoBehaviour
{
    public LazyBigus playa;

    private Collider2D beamCollider;
    private readonly HashSet<Character> hitTargets = new HashSet<Character>();

    private void Awake()
    {
        beamCollider = GetComponent<Collider2D>();
    }

    private void OnEnable()
    {
        hitTargets.Clear();

        if (beamCollider != null)
        {
            beamCollider.enabled = false;
        }

        StartCoroutine(BeamLifetime(1f));
    }

    public void ActivateCollider()
    {
        if (beamCollider != null)
        {
            beamCollider.enabled = true;

            // Προαιρετικό: άμεσο check για κάποιον που είναι ήδη μέσα
            CheckOverlappingPlayers();
        }
    }

    public void DeactivateCollider()
    {
        if (beamCollider != null)
        {
            beamCollider.enabled = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        TryHit(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        TryHit(collision);
    }

    private void TryHit(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Character target = GetCharacterFromCollider(collision);
            if (target == null || playa == null || !playa.CanDamageTarget(target))
            {
                return;
            }
            if (hitTargets.Add(target))
            {
                playa.BeamHit(target);
            }
        }
    }

    private Character GetCharacterFromCollider(Collider2D collision)
    {
        if (collision == null)
        {
            return null;
        }

        Character target = collision.GetComponent<Character>();
        if (target == null)
        {
            target = collision.GetComponentInParent<Character>();
        }

        return target;
    }

    private void CheckOverlappingPlayers()
    {
        if (beamCollider == null) return;

        Collider2D[] hits = Physics2D.OverlapBoxAll(
            beamCollider.bounds.center,
            beamCollider.bounds.size,
            0f
        );

        foreach (Collider2D hit in hits)
        {
            if (hit == beamCollider || !hit.CompareTag("Player"))
            {
                continue;
            }

            Character target = GetCharacterFromCollider(hit);
            if (target == null || playa == null || !playa.CanDamageTarget(target))
            {
                continue;
            }
            if (hitTargets.Add(target))
            {
                playa.BeamHit(target);
            }
        }
    }

    private IEnumerator BeamLifetime(float delay)
    {
        yield return new WaitForSeconds(delay);
        gameObject.SetActive(false);
    }
}
