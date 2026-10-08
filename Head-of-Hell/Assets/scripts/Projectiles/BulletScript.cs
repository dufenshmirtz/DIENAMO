using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Grpc.Core.ChannelOption;

public class BulletScript : MonoBehaviour
{
    private bool hasHit = false;
    private bool fireLogged = false;
    private Coroutine lifetimeCoroutine;

    public LazyBigus initiator;

    public void Init(LazyBigus owner)
    {
        initiator = owner;

        if (fireLogged) return;

        fireLogged = true;

        if (lifetimeCoroutine != null)
        {
            StopCoroutine(lifetimeCoroutine);
        }
        lifetimeCoroutine = StartCoroutine(DestroyAfterLifetime(2f));
    }

    private IEnumerator DestroyAfterLifetime(float lifetime)
    {
        yield return new WaitForSeconds(lifetime);

        if (!hasHit && initiator != null)
        {
            TelemetryManager.Instance?.LogMiss(initiator.PlayerId, MoveType.Quick);
        }

        Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !hasHit)
        {
            Character character = other.GetComponent<Character>();
            if (character == null)
            {
                character = other.GetComponentInParent<Character>();
            }
            if (character == null || initiator == null || !initiator.CanDamageTarget(character))
            {
                return;
            }

            hasHit = true;
            Destroy(gameObject);

            if (character != null)
            {
                TelemetryManager.Instance?.LogHitAttempt(
                    initiator.PlayerId,
                    character.PlayerId,
                    MoveType.Quick
                );

                character.SetIncomingDamageContext(
                    initiator.PlayerId,
                    MoveType.Quick,
                    SourceType.Projectile
                );

                character.TakeDamage(3, true);
                initiator.StackPoison(character);
            }
        }
    }
}
