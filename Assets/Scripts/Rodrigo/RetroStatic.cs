using UnityEngine;

public class RetroStatic : MonoBehaviour
{

    [Range(3, 50)]public static int FPS;
    [SerializeField, Range(3, 50)] private int _FPS = 40;

    [Range(1, 120)]public static int Pixel;
    [SerializeField, Range(1, 120)] private int Pixels = 2;

    private int Width;
    private int Height;

    [Header("Texture")]
    [SerializeField] private RenderTexture rt;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Application.targetFrameRate = _FPS;
    }

    // Update is called once per frame
    void Update()
    {
        int WidthMax = 1920;
        int HeightMax = 1080;

        Width = WidthMax/Pixels;
        Height = HeightMax/Pixels;

        Application.targetFrameRate = _FPS;
        if (FPS != _FPS)
        {
            FPS = _FPS;
        }
        else
        {
            _FPS = FPS;
        }
        if(Pixel != Pixels)
        {
            Pixelz(Width,Height);
            Pixel = Pixels;
        }
        else
        {
            Pixels = Pixel;
        }


        if(Input.GetKeyDown(KeyCode.Space))
        {
            Pixelz(Width,Height);
            Debug.Log("Cambio");
        }

    }
    void Pixelz(int Width, int Height)
    {
        rt.Release();
        rt.width = Width;        
        rt.height = Height;
        rt.filterMode = FilterMode.Point;
        rt.Create();
    }
    void Awake()
    {

    }
} 
