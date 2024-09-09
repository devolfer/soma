using UnityEngine;

namespace devolfer.Sound
{
    // TODO Remove this after testing
    public class Test : MonoBehaviour
    {
        [SerializeField] private AudioSource _source;
        
        [SerializeField] private SoundEmitter _soundEmitter;

        private SoundEntity _entity;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.P)) _soundEmitter.Play();

            if (Input.GetKeyDown(KeyCode.O)) _soundEmitter.Stop();

            if (Input.GetKeyDown(KeyCode.I)) _soundEmitter.Pause();

            if (Input.GetKeyDown(KeyCode.U)) _soundEmitter.Resume();

            if (Input.GetKeyDown(KeyCode.L)) _entity = SoundManager.Instance.Play(_source, onComplete: () => _entity = null);

            if (Input.GetKeyDown(KeyCode.K)) SoundManager.Instance.StopAll();

            if (Input.GetKeyDown(KeyCode.J)) SoundManager.Instance.PauseAll();

            if (Input.GetKeyDown(KeyCode.H)) SoundManager.Instance.ResumeAll();

            if (Input.GetKeyDown(KeyCode.F)) SoundManager.Instance.Fade(_entity, 2, 0, Ease.InOutSine);

            if (Input.GetKeyDown(KeyCode.D)) SoundManager.Instance.Fade(_entity, 2, 1, Ease.InOutSine);
            
            if (Input.GetKeyDown(KeyCode.Z)) _entity = SoundManager.Instance.Play(_source, fadeIn: true, fadeInDuration: 2);
            
            if (Input.GetKeyDown(KeyCode.C))
            {
                SoundManager.Instance.Stop(_entity);
                _entity = null;
            }
        }
    }
}