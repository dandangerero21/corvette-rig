using UnityEngine;
using System.Collections.Generic;

public class FestivalSpeakerSystem : MonoBehaviour
{
    [Header("Music Settings")]
    [Tooltip("The music track you want to play across the whole track.")]
    public AudioClip backgroundMusic;
    [Range(0f, 1f)]
    public float volume = 0.8f;

    [Header("Speaker Placement (Auto-Spawn)")]
    [Tooltip("Assign the AI Path Root here to automatically spawn speakers along the track.")]
    public Transform pathRoot;
    [Tooltip("Distance in meters between each spawned speaker.")]
    public float speakerSpacing = 150f;

    [Header("3D Audio Settings")]
    [Tooltip("Distance at which the music starts fading out.")]
    public float minDistance = 30f;
    [Tooltip("Distance at which the music is completely silent. Should be greater than speakerSpacing so there are no dead zones.")]
    public float maxDistance = 200f;

    private List<AudioSource> spawnedSpeakers = new List<AudioSource>();

    void Start()
    {
        if (backgroundMusic == null)
        {
            Debug.LogWarning("[Festival Speakers] No background music clip assigned!");
            return;
        }

        if (pathRoot != null && pathRoot.childCount > 1)
        {
            SpawnSpeakersAlongPath();
        }
        else
        {
            Debug.LogWarning("[Festival Speakers] Path Root not assigned or doesn't have enough waypoints. Placing a single speaker at this object's position.");
            CreateSpeaker(transform.position, "Fallback Speaker");
        }

        // Play all speakers in perfect sync
        foreach (var source in spawnedSpeakers)
        {
            source.Play();
        }
        
        Debug.Log($"[Festival Speakers] Started playing {backgroundMusic.name} perfectly synced across {spawnedSpeakers.Count} speakers.");
    }

    void SpawnSpeakersAlongPath()
    {
        int childCount = pathRoot.childCount;
        Vector3[] waypoints = new Vector3[childCount];
        for (int i = 0; i < childCount; i++)
        {
            waypoints[i] = pathRoot.GetChild(i).position;
        }

        float accumulatedDistance = 0f;
        int speakerCount = 0;

        // Place a speaker exactly at the start line
        CreateSpeaker(waypoints[0], $"Track Speaker {speakerCount++}");

        for (int i = 0; i < waypoints.Length; i++)
        {
            Vector3 currentPoint = waypoints[i];
            // Loop back to the start if we are at the last waypoint
            Vector3 nextPoint = waypoints[(i + 1) % waypoints.Length]; 
            
            float segmentLength = Vector3.Distance(currentPoint, nextPoint);
            Vector3 direction = (nextPoint - currentPoint).normalized;

            float distanceCoveredOnSegment = 0f;

            while (accumulatedDistance + (segmentLength - distanceCoveredOnSegment) >= speakerSpacing)
            {
                float distanceNeeded = speakerSpacing - accumulatedDistance;
                distanceCoveredOnSegment += distanceNeeded;
                
                Vector3 spawnPos = currentPoint + direction * distanceCoveredOnSegment;
                CreateSpeaker(spawnPos, $"Track Speaker {speakerCount++}");
                
                accumulatedDistance = 0f; // Reset accumulator for the next speaker
            }

            // Add the remaining segment distance to the accumulator
            accumulatedDistance += (segmentLength - distanceCoveredOnSegment);
        }
    }

    void CreateSpeaker(Vector3 position, string speakerName)
    {
        GameObject speakerObj = new GameObject(speakerName);
        speakerObj.transform.position = position;
        speakerObj.transform.SetParent(this.transform); // Keep the hierarchy clean

        AudioSource source = speakerObj.AddComponent<AudioSource>();
        source.clip = backgroundMusic;
        source.loop = true;
        source.volume = volume;
        source.playOnAwake = false; // We will play them all at once later
        
        // 3D Audio Settings
        source.spatialBlend = 1.0f; // 100% 3D
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        // Linear drop-off creates a much more realistic "festival speaker" bleed effect over long distances
        source.rolloffMode = AudioRolloffMode.Linear;
        source.dopplerLevel = 0f; // Music shouldn't pitch-bend as you drive past it
        source.spread = 180f; // Wide spread prevents "grainy" panning distortion when passing very close at high speeds

        spawnedSpeakers.Add(source);
    }
}
