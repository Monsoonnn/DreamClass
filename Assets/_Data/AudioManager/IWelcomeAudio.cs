using System;

namespace DreamClass.Audio
{
    /// <summary>
    /// Interface for components that can play welcome audio.
    /// The AudioManager will discover and control these players.
    /// </summary>
    public interface IWelcomeAudioPlayer
    {
        /// <summary>
        /// Unique identifier for this audio player.
        /// Used to track which audio has been played in the session.
        /// </summary>
        string AudioId { get; }
        
        /// <summary>
        /// Plays the welcome audio sequence.
        /// </summary>
        void Play();
        
        /// <summary>
        /// Checks if this audio player is ready to play.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// If true, this audio plays only once per session.
        /// If false, it plays every time the scene loads.
        /// </summary>
        bool PlayOnce { get; }
    }
}