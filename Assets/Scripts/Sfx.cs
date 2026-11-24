// Small helpers the scripts share for their sounds: one-shots with a little variety, and loops that rise and fall with a level.
// Every call does nothing without a source or clip, so a script's sound fields can be left empty.

using System.Collections.Generic;
using UnityEngine;

public static class Sfx
{
    // Each source's own volume and pitch, as set in the scene, which the levels here are fractions of
    static readonly Dictionary<AudioSource, (float volume, float pitch)> settings = new();

    static (float volume, float pitch) Settings(AudioSource source)
    {
        if (!settings.TryGetValue(source, out var set))
        {
            set = (source.volume, source.pitch);
            settings[source] = set;
        }
        return set;
    }

    // Plays a source's own clip once from the start, its pitch nudged at random so repeats don't sound copied
    public static void Play(AudioSource source, float pitch = 1f, float jitter = 0.05f)
    {
        if (source == null || source.clip == null) return;
        var set = Settings(source);
        source.pitch = set.pitch * pitch * (1f + Random.Range(-jitter, jitter));
        source.volume = set.volume;
        source.Play();
    }

    // Plays a clip once over whatever the source is already playing, at a fraction of its volume
    public static void PlayOneShot(AudioSource source, AudioClip clip, float volume = 1f, float jitter = 0.05f)
    {
        if (source == null || clip == null) return;
        source.pitch = Settings(source).pitch * (1f + Random.Range(-jitter, jitter));
        source.PlayOneShot(clip, volume);
    }

    // Keeps a looping source at a level from 0 (silent) to 1 (its own volume) and a pitch, easing there so it never clicks;
    // it starts, from a random point so loops don't line up, when it rises from silence, and pauses once silent
    public static void Loop(AudioSource source, float level, float pitch = 1f, float ease = 10f)
    {
        if (source == null || source.clip == null) return;
        var set = Settings(source);
        float target = set.volume * Mathf.Clamp01(level);
        source.volume = Mathf.MoveTowards(source.volume, target, set.volume * ease * Time.deltaTime);
        source.pitch = set.pitch * pitch;
        if (source.volume > 0.0001f)
        {
            if (source.isPlaying) return;
            source.loop = true;
            // Paused part-way, it carries on from there; never started, it starts somewhere random
            if (source.time > 0f) source.UnPause();
            else
            {
                source.time = Random.Range(0f, source.clip.length * 0.9f);
                source.Play();
            }
        }
        else if (source.isPlaying) source.Pause();
    }

    // Silences a loop at once
    public static void Stop(AudioSource source)
    {
        if (source == null) return;
        source.Stop();
        source.volume = 0f;
    }
}
