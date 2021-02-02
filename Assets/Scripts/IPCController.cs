#define UNITY_IPC_SYNC
#define PYTHON_IPC_SYNC
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading.Tasks;
using UnityEngine;

class SectionProperties
{
    public int start;
    public int size;
    
    public SectionProperties(int _start, int _size)
    {
        start = _start;
        size = _size;
    }
    
    public SectionProperties()
    {
    }
    
};

class Memory
{
    public int locationCounter;
    public Hashtable layout = new Hashtable();
    
    public MemoryMappedFile file;
    public MemoryMappedViewAccessor cursor;
    
    public void AddSection(string sectionName, int sectionSize)
    {
        SectionProperties sectionProperties = new SectionProperties(locationCounter, sectionSize);
        layout.Add(sectionName, sectionProperties);
        locationCounter += sectionSize;
    }
    
    public int GetLocation(string sectionName)
    {
        int result;
        Debug.Assert(layout.ContainsKey(sectionName), sectionName + " doesn't exist in the shared memory specification");
        result =  ((SectionProperties)(layout[sectionName])).start;
        return result;
    }
    
    public string GenerateCSV()
    {
        string result = "Section Name, Start Byte, Size\n";
        
        foreach (DictionaryEntry section in layout)
        {
            SectionProperties sectionProperties = (SectionProperties)section.Value;
            result += section.Key + "," + sectionProperties.start + "," + sectionProperties.size + "\n";
        }
        
        return result;
    }
    
};


public class IPCController : MonoBehaviour
{
    public static int ipcControllerInstances = 0;
    public static IPCController singleton = null;
    public  bool on = false;
    
    public Car car;
    public CameraModule[] cameraModules;
    
    string memoryName = "ipc";
    Memory sharedMemory;
    
    // Use this for initialization
    
    void Awake()
    {
        Debug.Assert(ipcControllerInstances == 0, "Two instances of IPCController were created, only one is allowed.");
        Debug.Assert(singleton == null, "Two instances of IPCController were created, only one is allowed.");
        
        ipcControllerInstances = 1;
        singleton = this;
    }
    
    void Start ()
    {
        // HACK(KARAN): Force initialization of Car properties
        GameObject carGO = GameObject.FindGameObjectWithTag("SelfDrivingCar");
        car = carGO.GetComponent<Car>();
        car.Start();
        
        sharedMemory = new Memory();
        
        sharedMemory.AddSection("unityFlag", 1);
        sharedMemory.AddSection("pythonFlag", 1);
        sharedMemory.AddSection("carData", sizeof(float) * car.carControlsData.Length);
        sharedMemory.AddSection("pythonData", sizeof(float) * car.pythonData.Length);
        
        //NOTE(KARAN) : Reserve memory for images captured from all cameras
        GameObject[] customCameras = GameObject.FindGameObjectsWithTag("CustomCamera");
        cameraModules = new CameraModule[customCameras.Length];
        for(int i = 0; i < cameraModules.Length; i++)
        {
            cameraModules[i] = customCameras[i].GetComponent<CameraModule>();
            
            // HACK(KARAN): Force initialization of each camera
            cameraModules[i].Start();
            
            int imageWidth = cameraModules[i].settings.imageWidth;
            int imageHeight = cameraModules[i].settings.imageHeight;
            int bytesPerPixel = cameraModules[i].settings.bytesPerPixel;
            int imageSize = imageHeight * imageWidth * bytesPerPixel;
            string cameraName = cameraModules[i].name;
            
            sharedMemory.AddSection(cameraName + "ImageWidth", sizeof(int));
            sharedMemory.AddSection(cameraName + "ImageHeight", sizeof(int));
            sharedMemory.AddSection(cameraName + "BytesPerPixel", sizeof(int));
            sharedMemory.AddSection(cameraName, imageSize);
        }
        
		
        //NOTE(KARAN) : Save the memory layout as a csv so that python can use it.
        string layoutCSV = sharedMemory.GenerateCSV();
        File.WriteAllText(Application.dataPath + "\\sharedMemoryLayout.csv", layoutCSV);
        
        sharedMemory.file = MemoryMappedFile.CreateOrOpen(memoryName, sharedMemory.locationCounter, MemoryMappedFileAccess.ReadWrite, MemoryMappedFileOptions.None, HandleInheritability.Inheritable);
        
        sharedMemory.cursor = sharedMemory.file.CreateViewAccessor();
        sharedMemory.cursor.Write(sharedMemory.GetLocation("unityFlag"), (byte)0);
    }
    
    
    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.F1))
        {
            on = !on;
        }
        
        if(on)
        {
            
            byte unityFlag = sharedMemory.cursor.ReadByte(sharedMemory.GetLocation("unityFlag"));
#if UNITY_IPC_SYNC
            if (unityFlag == 0)
            {
#endif
                //NOTE(KARAN) : Upload car data
                sharedMemory.cursor.WriteArray<float>(sharedMemory.GetLocation("carData"), car.carControlsData, 0, car.carControlsData.Length);
                
                //NOTE(KARAN) : Upload each camera's images
                //TODO(KARAN) : Consider making this async
                for (int i = 0; i < cameraModules.Length; i++)
                {
                    CameraModule cm = cameraModules[i];
                    
                    
                    int imageWidth = cameraModules[i].settings.imageWidth;
                    int imageHeight = cameraModules[i].settings.imageHeight;
                    int bytesPerPixel = cameraModules[i].settings.bytesPerPixel;
                    
                    sharedMemory.cursor.Write(sharedMemory.GetLocation(cm.name + "ImageWidth"), imageWidth);
                    sharedMemory.cursor.Write(sharedMemory.GetLocation(cm.name + "ImageHeight"), imageHeight);
                    sharedMemory.cursor.Write(sharedMemory.GetLocation(cm.name + "BytesPerPixel"), bytesPerPixel);
                    
                    sharedMemory.cursor.WriteArray<byte>(sharedMemory.GetLocation(cm.name), cm.capturedImage, 0, cm.capturedImage.Length);
                }
                
                //NOTE(KARAN) : Set unity flag to 1 indicating new data for consumption is available
                sharedMemory.cursor.Write(sharedMemory.GetLocation("unityFlag"), (byte)1);
#if UNITY_IPC_SYNC
            }
#endif
            
#if PYTHON_IPC_SYNC
            byte pythonFlag = sharedMemory.cursor.ReadByte(sharedMemory.GetLocation("pythonFlag"));
            if (pythonFlag == 1)
            {
#endif
                //NOTE(KARAN) : Get python data
                int read = sharedMemory.cursor.ReadArray<float>(sharedMemory.GetLocation("pythonData"), car.pythonData, 0, car.pythonData.Length);
                
                //NOTE(KARAN) : Set python flag to 0 indicating we read python's decision.
                sharedMemory.cursor.Write(sharedMemory.GetLocation("pythonFlag"), (byte)0);
                
#if PYTHON_IPC_SYNC
            }
#endif
        }
    }
    
    private void OnApplicationQuit()
    {
        sharedMemory.file.Dispose();
    }
    
}
