using UnityEngine;

public class InputController : MonoBehaviour
{
    private float _jumpBtn = 0;

    private Vector2 _startPos;
    private Vector2 _dragPos;
    private Vector2 _endPos;
    
    public float touchSensitivityMinY = 1f;
    public float touchSensitivityMaxY = 4f;

    private float _touchSensitivityY = 1;

    public float touchSensitivityX = 50;
    
    private BuletLuncher _buletLuncher;


    void Start()
    {

        
        _buletLuncher = GetComponent<BuletLuncher>();
        
        _touchSensitivityY = touchSensitivityMaxY;
    }
    
    void Update()
    {
        _jumpBtn = Input.GetAxisRaw("Jump");
        
        float windowWidth = Screen.width;
        float windowHeight = Screen.height;

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    _startPos = touch.position;
                    break;
                case TouchPhase.Moved:
                    _dragPos = touch.position - _startPos;

                    // could use clamp and some low value number for this
                    _dragPos.x = (_dragPos.x / windowWidth) * touchSensitivityX;
                    _touchSensitivityY = Mathf.Lerp(touchSensitivityMaxY, touchSensitivityMinY , _dragPos.y / windowHeight);
                    _dragPos.y = Mathf.Clamp(((_dragPos.y / windowHeight) * _touchSensitivityY * 60) + 60, 0, 60);

                    _buletLuncher.RotateCatapultHead(_dragPos.y);

                    _buletLuncher.RotateCatapultBody(_dragPos.x);
                    
                    
                    
                    break;
                case TouchPhase.Ended:
                    _endPos = touch.position;
                    _buletLuncher.ResetCatapultHeadRotation();
                    // _buletLuncher.ResetCatapultBodyRotation();
                    break;
            }
            
        }
    }

    public float GetJumpBtn()
    {
        return _jumpBtn;
    }

}
