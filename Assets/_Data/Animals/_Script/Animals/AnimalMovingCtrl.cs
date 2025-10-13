using UnityEngine;
namespace GrabQuiz.Animals {
    public class AnimalMovingCtrl : NewMonobehavior {

        public Animator m_animator;
        public AnimalCtrl animalCtrl;
        public float m_speed = 1.0f;
        public float m_rotationSpeed = 5.0f;

        public Transform m_appearPos;
        public Transform m_disappearPos;

        private Transform targetPos;
        private bool isMoving = false;

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadAnimator();
            this.LoadAnimalCtrl();
        }

        protected virtual void LoadAnimalCtrl() { 
            if (this.animalCtrl != null) return;
            this.animalCtrl = this.GetComponentInParent<AnimalCtrl>();
            Debug.Log(transform.name + " LoadAnimalCtrl: " + this.animalCtrl);
        } 

        protected virtual void LoadAnimator() {
            if (this.m_animator != null) return;
            this.m_animator = this.GetComponent<Animator>();
            Debug.Log(transform.name + " LoadAnimator: " + this.m_animator);
        }

        private void Update() {
            if (isMoving) {
                Move();
            }
        }

        public void StartMoving( bool toAppear ) {
            if (m_appearPos == null || m_disappearPos == null) return;

            targetPos = toAppear ? m_appearPos : m_disappearPos;
            isMoving = true;
            m_animator.CrossFade(ConstAnimatorAnimal.walk.ToString(), 0.1f);
        }

        private void Move() {
            if (targetPos == null) return;

            RotateTowards(targetPos);
            transform.parent.position = Vector3.MoveTowards(transform.parent.position, targetPos.position, m_speed * Time.deltaTime);

            if (Vector3.Distance(transform.position, targetPos.position) < 0.1f) {
                isMoving = false;
                m_animator.CrossFade(ConstAnimatorAnimal.idle.ToString(), 0.1f);
                if(targetPos == m_disappearPos) this.animalCtrl.gameObject.SetActive(false);
            }
        }

        private void RotateTowards( Transform target ) {
            Vector3 direction = (target.position - transform.position).normalized;
            Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
            transform.rotation = Quaternion.Lerp(transform.rotation, lookRotation, Time.deltaTime * m_rotationSpeed);
        }
    }
}
    

