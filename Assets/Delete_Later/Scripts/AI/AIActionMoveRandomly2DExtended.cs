using UnityEngine;
using MoreMountains.TopDownEngine;

namespace RingOfEldenSwords.AI
{
    /// <summary>
    /// Extends AIActionMoveRandomly2D so that when a wall is detected,
    /// the new direction is guaranteed to face AWAY from the wall using
    /// the hit normal — preventing enemies from walking through boundary walls.
    /// </summary>
    [AddComponentMenu("TopDown Engine/Character/AI/Actions/AI Action Move Randomly 2D Extended")]
    public class AIActionMoveRandomly2DExtended : AIActionMoveRandomly2D
    {
        protected override void CheckForObstacles()
        {
            // Throttle: skip this check if not enough time has passed since the last obstacle detection.
            // Prevents firing a physics BoxCast every single frame — only runs once per ObstaclesCheckFrequency seconds.
            if (Time.time - _lastObstacleDetectionTimestamp < ObstaclesCheckFrequency)
                return;

            // Use ObstaclesDetectionDistance instead of _direction.magnitude
            // so the enemy detects walls early enough to turn before overlapping
            RaycastHit2D hit = Physics2D.BoxCast(
                _collider.bounds.center,
                _collider.bounds.size,
                0f,
                _direction.normalized,
                ObstaclesDetectionDistance,
                ObstacleLayerMask);

            if (hit)
                PickDirectionAwayFromWall(hit.normal);

            _lastObstacleDetectionTimestamp = Time.time;
        }

        /// <summary>
        /// Reflects direction off the wall, then checks if the reflected path is ALSO
        /// blocked (corner case). If so, combines both normals to escape diagonally.
        /// </summary>
        protected virtual void PickDirectionAwayFromWall(Vector2 wallNormal)
        {
            // Reflect off the wall we hit
            Vector2 reflected = Vector2.Reflect(_direction, wallNormal);

            // Check if the reflected direction is ALSO blocked (we're in a corner)
            RaycastHit2D secondHit = Physics2D.BoxCast(
                _collider.bounds.center,
                _collider.bounds.size,
                0f,
                reflected.normalized,
                ObstaclesDetectionDistance,
                ObstacleLayerMask);

            if (secondHit)
            {
                // Corner: combine both wall normals → points diagonally away from corner
                Vector2 escape = (wallNormal + secondHit.normal).normalized;
                _direction = escape;
            }
            else
            {
                _direction = reflected;
            }

            _lastDirectionChangeTimestamp = Time.time;
        }
    }
}