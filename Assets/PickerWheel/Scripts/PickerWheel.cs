using UnityEngine ;
using UnityEngine.UI ;
using DG.Tweening ;
using UnityEngine.Events ;
using System.Collections.Generic ;

namespace EasyUI.PickerWheelUI {

   public class PickerWheel : MonoBehaviour {

      [Header ("References :")]
      [SerializeField] private GameObject linePrefab ;
      [SerializeField] private Transform linesParent ;

      [Space]
      [SerializeField] private Transform PickerWheelTransform ;
      [SerializeField] private Transform wheelCircle ;
      [SerializeField] private GameObject wheelPiecePrefab ;
      [SerializeField] private Transform wheelPiecesParent ;

      [Space]
      [Header ("Sounds :")]
      [SerializeField] private AudioSource audioSource ;
      [SerializeField] private AudioClip tickAudioClip ;
      [SerializeField] private AudioClip successAudioClip ;
      [SerializeField] [Range (0f, 1f)] private float volume = .5f ;
      [SerializeField] [Range (-3f, 3f)] private float pitch = 1f ;

      [Space]
      [Header ("Picker wheel settings :")]
      [Range (1, 20)] public int spinDuration = 8 ;
      [SerializeField] [Range (.2f, 2f)] private float wheelSize = 1f ;

      [Space]
      [Header ("Picker wheel pieces :")]
      public WheelPiece[] wheelPieces ;

      // Events
      private UnityAction onSpinStartEvent ;
      private UnityAction<WheelPiece> onSpinEndEvent ;


      private bool _isSpinning = false ;

      public bool IsSpinning { get { return _isSpinning ; } }


      private Vector2 pieceMinSize = new Vector2 (81f, 146f) ;
      private Vector2 pieceMaxSize = new Vector2 (144f, 213f) ;
      private int piecesMin = 2 ;
      private int piecesMax = 12 ;

      private float pieceAngle ;
      private float halfPieceAngle ;
      private float halfPieceAngleWithPaddings ;


      private double accumulatedWeight ;
      private System.Random rand = new System.Random () ;

      private List<int> nonZeroChancesIndices = new List<int> () ;
      
      private bool isInitialized = false;

      private void Start () {
         // Không generate ở Start nữa, đợi SetupWheel() từ bên ngoài
         SetupAudio () ;
      }

      private void SetupAudio () {
         audioSource.clip = tickAudioClip ;
         audioSource.volume = volume ;
         audioSource.pitch = pitch ;
      }
      
      /// <summary>
      /// Khởi tạo wheel sau khi wheelPieces đã được set từ bên ngoài
      /// Gọi method này sau khi set wheelPieces array
      /// </summary>
      public void InitializeWheel() {
         if (isInitialized) {
            Debug.LogWarning("[PickerWheel] Wheel already initialized. Skipping.");
            return;
         }
         
         if (wheelPieces == null || wheelPieces.Length == 0) {
            Debug.LogError("[PickerWheel] wheelPieces is null or empty. Cannot initialize.");
            return;
         }
         
         pieceAngle = 360 / wheelPieces.Length ;
         halfPieceAngle = pieceAngle / 2f ;
         halfPieceAngleWithPaddings = halfPieceAngle - (halfPieceAngle / 4f) ;

         Generate () ;  

         CalculateWeightsAndIndices () ;
         if (nonZeroChancesIndices.Count == 0)
            Debug.LogWarning ("[PickerWheel] All pieces have zero chance") ;
         
         isInitialized = true;
         Debug.Log($"[PickerWheel] Initialized with {wheelPieces.Length} pieces");
      }

      private void Generate () {
         wheelPiecePrefab = InstantiatePiece () ;

         RectTransform rt = wheelPiecePrefab.transform.GetChild (0).GetComponent <RectTransform> () ;
         float pieceWidth = Mathf.Lerp (pieceMinSize.x, pieceMaxSize.x, 1f - Mathf.InverseLerp (piecesMin, piecesMax, wheelPieces.Length)) ;
         float pieceHeight = Mathf.Lerp (pieceMinSize.y, pieceMaxSize.y, 1f - Mathf.InverseLerp (piecesMin, piecesMax, wheelPieces.Length)) ;
         rt.SetSizeWithCurrentAnchors (RectTransform.Axis.Horizontal, pieceWidth) ;
         rt.SetSizeWithCurrentAnchors (RectTransform.Axis.Vertical, pieceHeight) ;

         for (int i = 0; i < wheelPieces.Length; i++)
            DrawPiece (i) ;

         Destroy (wheelPiecePrefab) ;
      }

      private void DrawPiece (int index) {
         WheelPiece piece = wheelPieces [ index ] ;
         Transform pieceTrns = InstantiatePiece ().transform.GetChild (0) ;

         pieceTrns.GetChild (0).GetComponent <Image> ().sprite = piece.Icon ;
         pieceTrns.GetChild (1).GetComponent <Text> ().text = piece.Label ;

         //Line
         Transform lineTrns = Instantiate (linePrefab, linesParent.position, Quaternion.identity, linesParent).transform ;
         lineTrns.RotateAround (wheelPiecesParent.position, Vector3.back, (pieceAngle * index) + halfPieceAngle) ;

         pieceTrns.RotateAround (wheelPiecesParent.position, Vector3.back, pieceAngle * index) ;
      }

      private GameObject InstantiatePiece () {
         return Instantiate (wheelPiecePrefab, wheelPiecesParent.position, Quaternion.identity, wheelPiecesParent) ;
      }


      public void Spin (int targetIndex) {
         if (!isInitialized) {
            Debug.LogError("[PickerWheel] Wheel not initialized. Call InitializeWheel() first!");
            return;
         }
         
         if (!_isSpinning) {
            _isSpinning = true ;
            if (onSpinStartEvent != null)
               onSpinStartEvent.Invoke () ;

            int index = targetIndex ;
            WheelPiece piece = wheelPieces [ index ] ;

            if (piece.Chance == 0 && nonZeroChancesIndices.Count != 0) {
               index = nonZeroChancesIndices [ Random.Range (0, nonZeroChancesIndices.Count) ] ;
               piece = wheelPieces [ index ] ;
            }

            float angle = -(pieceAngle * index) ;

            float rightOffset = (angle - halfPieceAngleWithPaddings) % 360 ;
            float leftOffset = (angle + halfPieceAngleWithPaddings) % 360 ;

            float randomAngle = Random.Range (leftOffset, rightOffset) ;

            Vector3 targetRotation = new Vector3(0, 0, -(randomAngle + 2 * 360 * spinDuration));

            //float prevAngle = wheelCircle.eulerAngles.z + halfPieceAngle ;
            float prevAngle, currentAngle ;
            prevAngle = currentAngle = wheelCircle.eulerAngles.z ;

            bool isIndicatorOnTheLine = false ;

            wheelCircle
            .DORotate (targetRotation, spinDuration, RotateMode.FastBeyond360)
            .SetEase (Ease.InOutQuart)
            .OnUpdate (() => {
               float diff = Mathf.Abs (prevAngle - currentAngle) ;
               if (diff >= halfPieceAngle) {
                  if (isIndicatorOnTheLine) {
                     audioSource.PlayOneShot (audioSource.clip) ;
                  }
                  prevAngle = currentAngle ;
                  isIndicatorOnTheLine = !isIndicatorOnTheLine ;
               }
               currentAngle = wheelCircle.eulerAngles.z ;
            })
            .OnComplete (() => {
               _isSpinning = false ;
               
               // Play success sound
               if (successAudioClip != null) {
                  audioSource.PlayOneShot (successAudioClip, volume) ;
               }
               
               if (onSpinEndEvent != null)
                  onSpinEndEvent.Invoke (piece) ;

               onSpinStartEvent = null ; 
               onSpinEndEvent = null ;
            }) ;

         }
      }


      public void OnSpinStart (UnityAction action) {
         onSpinStartEvent = action ;
      }

      public void OnSpinEnd (UnityAction<WheelPiece> action) {
         onSpinEndEvent = action ;
      }


      private int GetRandomPieceIndex () {
         double r = rand.NextDouble () * accumulatedWeight ;

         for (int i = 0; i < wheelPieces.Length; i++)
            if (wheelPieces [ i ]._weight >= r)
               return i ;

         return 0 ;
      }

      private void CalculateWeightsAndIndices () {
         for (int i = 0; i < wheelPieces.Length; i++) {
            WheelPiece piece = wheelPieces [ i ] ;

            //add weights:
            accumulatedWeight += piece.Chance ;
            piece._weight = accumulatedWeight ;

            //add index :
            piece.Index = i ;

            //save non zero chance indices:
            if (piece.Chance > 0)
               nonZeroChancesIndices.Add (i) ;
         }
      }




      private void OnValidate () {
         if (PickerWheelTransform != null)
            PickerWheelTransform.localScale = new Vector3 (wheelSize, wheelSize, 1f) ;

         // Chỉ warning khi wheel pieces thực sự invalid (không phải empty array lúc mới tạo)
         if (wheelPieces != null && wheelPieces.Length > 0) {
            if (wheelPieces.Length > piecesMax || wheelPieces.Length < piecesMin)
               Debug.LogWarning ($"[PickerWheel] Pieces count ({wheelPieces.Length}) should be between {piecesMin} and {piecesMax} for optimal display.", this) ;
         }
      }
   }
}