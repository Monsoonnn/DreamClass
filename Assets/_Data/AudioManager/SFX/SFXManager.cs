using UnityEngine;
using System.Collections.Generic;
using com.cyborgAssets.inspectorButtonPro;

namespace AudioManager
{

    [RequireComponent(typeof(AudioSource))]
    public class SFXManager : SingletonCtrl<SFXManager>
    {
        [Header("SFX Data")]
        [SerializeField] private List<SFXData> sfxDataList;
        public float totalVolume = 1f;
        [SerializeField] private bool debugMode = true;

        // Dictionary tra cứu nhanh theo id và name
        private Dictionary<string, SFXData> sfxById = new Dictionary<string, SFXData>();
        private Dictionary<string, SFXData> sfxByName = new Dictionary<string, SFXData>();
        private AudioSource audioSource;
        protected override void Start()
        {
            base.Start();
            BuildSFXDictionaries();
            ClearAudioClip();
        }

        private void ClearAudioClip()
        {
            if (audioSource != null)
            {
                audioSource.clip = null;
            }
        }

        private void BuildSFXDictionaries()
        {
            sfxById.Clear();
            sfxByName.Clear();
            if (sfxDataList == null) return;
            foreach (var sfx in sfxDataList)
            {
                if (!string.IsNullOrEmpty(sfx.id))
                    sfxById[sfx.id] = sfx;
                if (!string.IsNullOrEmpty(sfx.name))
                    sfxByName[sfx.name] = sfx;
            }
            if (debugMode)
                Debug.Log($"[SFXManager] Built SFX dictionaries: {sfxById.Count} by id, {sfxByName.Count} by name");
        }

        protected override void LoadComponents()
        {
            base.LoadComponents();
            if (audioSource == null)audioSource = GetComponent<AudioSource>();
        }

        /// <summary>
        /// Play SFX bằng ID
        /// </summary>
        [ProButton]
        public void PlaySFXByID(string sfxId)
        {
            if (sfxById.TryGetValue(sfxId, out var sfxData))
            {
                if (sfxData.audioClip != null)
                {
                    float mappedVolume = Mathf.Lerp(0.1f, 0.5f, Mathf.Clamp01(totalVolume));
                    audioSource.PlayOneShot(sfxData.audioClip, mappedVolume);
                    if (debugMode)
                        Debug.Log($"[SFXManager] Playing SFX: {sfxId} (mappedVolume={mappedVolume:F2})");
                }
                else
                {
                    Debug.LogWarning($"[SFXManager] AudioClip is null for SFX: {sfxId}");
                }
            }
            else
            {
                Debug.LogWarning($"[SFXManager] SFX not found: {sfxId}");
            }
        }

        /// <summary>
        /// Play SFX bằng tên (tìm ID tương ứng)
        /// </summary>
        [ProButton]
        public void PlaySFXByName(string sfxName)
        {
            if (sfxByName.TryGetValue(sfxName, out var sfxData))
            {
                PlaySFXByID(sfxData.id);
            }
            else
            {
                Debug.LogWarning($"[SFXManager] SFX not found by name: {sfxName}");
            }
        }

        /// <summary>
        /// Kiểm tra SFX có tồn tại không
        /// </summary>
        public bool HasSFX(string sfxId)
        {
            return sfxById.ContainsKey(sfxId);
        }

        /// <summary>
        /// Lấy AudioClip bằng ID
        /// </summary>
        public AudioClip GetSFX(string sfxId)
        {
            if (sfxById.TryGetValue(sfxId, out var sfxData))
                return sfxData.audioClip;
            return null;
        }

        /// <summary>
        /// Dừng âm thanh hiện tại
        /// </summary>
        public void StopSFX()
        {
            audioSource.Stop();
        }

        /// <summary>
        /// Set volume cho SFX Manager
        /// </summary>
        public void SetVolume(float volume)
        {
            audioSource.volume = Mathf.Clamp01(volume);
        }

        public float GetVolume()
        {
            return audioSource.volume;
        }
    }

}