using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.InteropServices;
using System.Threading;
using System;
using System.Reflection;


[System.Serializable]
public class Settings
{
    public bool useDefault = true;
    public int imageWidth;
    public int imageHeight;
    public int bytesPerPixel;
    public int numFramesToBuffer;
    public int captureFPS;
};


public class RawHeader
{
    public char[] magicValue = { 'r', 'a', 'w' };
    public int versionNumber = 0;
    public int numImages;
    public int imageWidth;
    public int imageHeight;
    public int bytesPerPixel;
    public int imageStartOffset;
	
	public RawHeader()
	{
		imageStartOffset = this.ToByteArray().Length;
	}
    
    public byte[] ToByteArray()
    {
        List<byte> serialized = new List<byte>();
        
        for (int i = 0; i < magicValue.Length; i++)
        {
            serialized.Add((byte)magicValue[i]);
        }
        
        serialized.AddRange(Utility.ConvertIntToByte(versionNumber));
        serialized.AddRange(Utility.ConvertIntToByte(numImages));
        serialized.AddRange(Utility.ConvertIntToByte(imageWidth));
        serialized.AddRange(Utility.ConvertIntToByte(imageHeight));
        serialized.AddRange(Utility.ConvertIntToByte(bytesPerPixel));
        serialized.AddRange(Utility.ConvertIntToByte(imageStartOffset));
        
        return serialized.ToArray();
    }
    
};


public class Utility
{
    public static byte[] ConvertIntToByte(int a)
    {
        byte[] result = new byte[4];
        
        result[0] = (byte)(a & 0x000000FF);
        result[1] = (byte)((a >> 8) & 0x000000FF);
        result[2] = (byte)((a >> 16) & 0x000000FF);
        result[3] = (byte)((a >> 24) & 0x000000FF);
        
        return result;
    }
}

public class DoubleBuffer
{
    public int capacitySize = 0;
    public byte[][] buffers;
	public int[] fillLevels;
    
    public int activeBufferIndex;
    public DoubleBuffer(int _capacitySize)
    {
        capacitySize = _capacitySize;
        buffers = new byte[2][];
        buffers[0] = new byte[capacitySize];
        buffers[1] = new byte[capacitySize];
		
		fillLevels = new int[2];
		fillLevels[0] = 0;
		fillLevels[1] = 0;
    }
    
    public void PushData(byte[] data)
    {
        Debug.Assert(fillLevels[activeBufferIndex] + data.Length <= capacitySize, "Double buffer out of bounds push.");
        
        data.CopyTo(buffers[activeBufferIndex], fillLevels[activeBufferIndex]);
        fillLevels[activeBufferIndex] += data.Length;
        
        if (fillLevels[activeBufferIndex] >= capacitySize)
        {
            activeBufferIndex = (activeBufferIndex + 1) % 2;
            fillLevels[activeBufferIndex] = 0;
        }
    }
	
	
    public bool IsFull()
    {
		bool result = (fillLevels[0] == capacitySize) || (fillLevels[1] == capacitySize);
        return result;
    }
    
    public bool IsNotEmpty()
    {
        bool result;
        result = fillLevels[0] > 0 || fillLevels[1] > 0;
        return result;
    }
    
    public byte[] FlushAndGetAllData()
    {
        
        int dataSize = fillLevels[0] + fillLevels[1];
        
        byte[] result = new byte[dataSize];
        
        int firstBufferIndex = fillLevels[0] > fillLevels[1] ? 0 : 1;
        int secondBufferIndex = (firstBufferIndex + 1) % 2;
        
        int filled = 0;
        
        if(fillLevels[firstBufferIndex] > 0)
        {
            Array.Copy(buffers[firstBufferIndex], 0, result, filled, fillLevels[firstBufferIndex]);
            filled += fillLevels[firstBufferIndex];
        }
        if(fillLevels[secondBufferIndex] > 0)
        {
            Array.Copy(buffers[secondBufferIndex], 0, result, filled, fillLevels[secondBufferIndex]);
            filled += fillLevels[secondBufferIndex];
        }
        
        fillLevels[0] = 0;
        fillLevels[1] = 0;
        
        return result;
    }
    
    public byte[] FlushAndGetFilledBuffer()
    {
		int filledBufferIndex = -1;
		if(fillLevels[0] == capacitySize)
		{
			filledBufferIndex = 0;
		}
		else if(fillLevels[1] == capacitySize)
		{
			filledBufferIndex = 1;
		}
        Debug.Assert(filledBufferIndex != -1, "Tried to retrieve a filled buffer but there was no filled buffer.");
		
		fillLevels[filledBufferIndex] = 0;
        return buffers[filledBufferIndex];
    }
    
}

public class CameraModule : MonoBehaviour {
    
    public bool on = false;
    
    public Settings settings = new Settings();
    RawHeader rawHeader;
    Camera cameraComponent;
	byte[] rawHeaderSerialized;
    DoubleBuffer doubleBuffer;
	int fileNum = 0;
    Task dispatchedTask;
    bool firstPassOfDoubleBuffering = true;
	
	float sleepClock = 0.0f;
	float secondsPerImage;
	float debugElapsedTime = 0.0f;
	float capturedFrames = 0.0f;
	
    Texture2D image;
    Rect imageRect;
    
    string pathOutsideAssetsFolder;
    
    string capturedImagesDirectory;
	public byte[] capturedImage;
    
    string debugShutterRateData = "FrameNumber, Sleep, PrevFrameTime, Capture, NewSleep\n";
    //(Omkar:) Don't keep space between commas in heading, otherwise space needs to be added while loading .csv through pandas
    string debugCarControlsData = "Velocity,Steering,LeftDistance,RightDistance,VA,AngVel\n"; 
    int debugCarControlsFlushLength = 1024;
    
    
    // HACK(KARAN): StreamWriter and FileStream is temporary for saving car controls
    StreamWriter sw = null;
    FileStream fs = null;
    
    public void Start () 
    {
        // HACK(KARAN): This is temporary for saving car controls
        if(this.name == "Front" && sw == null)
        {
            sw = new StreamWriter(Application.dataPath + "\\CapturedImages~\\carControls.csv", false);
        }
        
        if(settings.useDefault) 
		{	
			settings.imageWidth = 256;
			settings.imageHeight = 256;
			settings.bytesPerPixel = 3;
			settings.numFramesToBuffer = 100;
			settings.captureFPS = 15;
		}
        
		secondsPerImage = (1.0f/settings.captureFPS);
        imageRect = new Rect(0, 0, settings.imageWidth, settings.imageHeight);
        
        /* Texture Formats: RGBA32, RGB24*/
        TextureFormat format = settings.bytesPerPixel == 3 ? TextureFormat.RGB24 : TextureFormat.RGBA32;
        image = new Texture2D(settings.imageWidth, settings.imageHeight, format, false);
        cameraComponent = GetComponent<Camera>();
        cameraComponent.targetTexture = new RenderTexture(settings.imageWidth, settings.imageHeight, 0, RenderTextureFormat.ARGB32);
        cameraComponent.enabled = false;
        
        // NOTE(KARAN): Setting up of double buffer for the async version.
        rawHeader = new RawHeader();
        rawHeader.imageHeight = settings.imageHeight;
        rawHeader.imageWidth = settings.imageWidth;
        rawHeader.bytesPerPixel = settings.bytesPerPixel;
        rawHeader.numImages = settings.numFramesToBuffer;
        rawHeaderSerialized = rawHeader.ToByteArray();
        
        int fileSize = rawHeader.imageWidth * rawHeader.imageHeight * rawHeader.bytesPerPixel * rawHeader.numImages;
        doubleBuffer = new DoubleBuffer(fileSize);
        
        capturedImagesDirectory = Application.dataPath + "\\CapturedImages~\\" + this.name;
        if (!Directory.Exists(capturedImagesDirectory))
        {
            Directory.CreateDirectory(capturedImagesDirectory);
        }
        
        
    }
	
    string GetTaskStatusString(Task t)
    {
        string result;
        if (t.Status == TaskStatus.Created)
        {
            result = "Created";
        }
        else if (t.Status == TaskStatus.WaitingForActivation)
        {
            result = "Waiting for activation";
        }
        else if (t.Status == TaskStatus.WaitingToRun)
        {
            result = "Waiting to run";
        }
        else if (t.Status == TaskStatus.Running)
        {
            result = "Running";
        }
        else if (t.Status == TaskStatus.WaitingForChildrenToComplete)
        {
            result = "Waiting for children to complete";
        }
        else if (t.Status == TaskStatus.RanToCompletion)
        {
            result = "Ran to completetion";
        }
        else if (t.Status == TaskStatus.Canceled)
        {
            result = "Cancelled";
        }
        else if (t.Status == TaskStatus.Faulted)
        {
            result = "Faulted";
        }
        else
        {
            result = "Cannot be determined";
        }
        
        return result;
    }
    
    
    void WriteRawFile(String filePath, RawHeader rawHeader,  byte[] rawHeaderSerialized, byte[] data)
    {
        using (var stream = new FileStream(filePath, FileMode.OpenOrCreate))
        {
            //byte[] rawHeaderSerialized = rawHeader.ToByteArray();
            stream.Write(rawHeaderSerialized, 0, rawHeaderSerialized.Length);
            int dataSize = rawHeader.numImages * rawHeader.imageWidth * rawHeader.imageHeight * rawHeader.bytesPerPixel;
            stream.Write(data, 0, dataSize);
            stream.Close();
        }
        //System.IO.File.WriteAllBytes(filePath, data);
    }
    
    void GetRenderedImage(Texture2D imageTexture, Rect rect)
    {
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = cameraComponent.targetTexture;
        imageTexture.ReadPixels(rect, 0, 0);
        imageTexture.Apply();
        RenderTexture.active = currentRT;
    }
    
    
    void CaptureCameraAsyncUsingTask()
    {
        
        
        int capture = 0;
		// TODO(KARAN): Check whether this is ACTUALLY capturing at the desired fps
		debugShutterRateData += Time.frameCount + "," + sleepClock + "," + Time.deltaTime + ",";
		if(sleepClock > 0.0f)
		{
			sleepClock -= Time.deltaTime;
		}
        
		if(sleepClock <= 0.0f)
		{
            capture = 1;
			capturedFrames++;
			GetRenderedImage(image, imageRect);
			doubleBuffer.PushData(image.GetRawTextureData());
			if (doubleBuffer.IsFull())
			{
				if (!firstPassOfDoubleBuffering)
				{
					dispatchedTask.Wait();
				}
                
				String filePath = capturedImagesDirectory + "\\" + (fileNum++) + ".raw";
				dispatchedTask = Task.Run(() => WriteRawFile(filePath, rawHeader, rawHeaderSerialized, doubleBuffer.FlushAndGetFilledBuffer()));
				firstPassOfDoubleBuffering = false;
			}
			
			if(Time.deltaTime < secondsPerImage)
			{
				sleepClock = (secondsPerImage - Time.deltaTime) + sleepClock;
			}
			else
			{
				sleepClock = 0.0f;
			}
            
            
            // HACK(KARAN): This is temporary for saving car controls
            if(this.name == "Front")
            {
                
                string s = "";
                int length = IPCController.singleton.car.carControlsData.Length;
                float[] carControls = IPCController.singleton.car.carControlsData;
                for(int i = 0; i < length - 1; i++)
                {
                    s += carControls[i] + ",";
                }
                s += carControls[length - 1];
                debugCarControlsData += s;
                if(debugCarControlsData.Length > 0)
                {
                    sw.WriteLine(debugCarControlsData);
                    debugCarControlsData = "";
                }
            }
        }
        debugShutterRateData += capture + "," + sleepClock + "\n";
        debugElapsedTime += Time.deltaTime;
    }
    
    
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.F2))
        {
            on = !on;
        }
        
        cameraComponent.enabled = on || IPCController.singleton.on;
    }
    
    void OnPostRender()
    {
        if (IPCController.singleton.on)
        {
            GetRenderedImage(image, imageRect);
            capturedImage = image.GetRawTextureData();
        }
        else if(on)
        {
            CaptureCameraAsyncUsingTask();
        }
        
    }
    
    void OnApplicationQuit()
    {
        // HACK(KARAN): This is temporary for saving car controls
        if(this.name == "Front")
        {
            if(debugCarControlsData.Length != 0)
            {
                sw.WriteLine(debugCarControlsData);
            }
            sw.Close();
        }
        
        if(doubleBuffer.IsNotEmpty())
        {
            String filePath = capturedImagesDirectory + "\\" + (fileNum++) + ".raw";
            byte[] remainingImages = doubleBuffer.FlushAndGetAllData();
            
            int imageSize = rawHeader.imageWidth * rawHeader.imageHeight * rawHeader.bytesPerPixel;
            
            rawHeader.numImages = remainingImages.Length/imageSize;
            
            rawHeaderSerialized = rawHeader.ToByteArray();
            WriteRawFile(filePath, rawHeader, rawHeaderSerialized, remainingImages);
        }
        
        System.IO.File.WriteAllText (capturedImagesDirectory + "\\" + this.name + "_debugCaptureFPS.csv", debugShutterRateData);
        
    }
}

/* OLD FORMAT

[System.Serializable]
public class CameraCaptureConfig
{
    public int imageWidth;
    public int imageHeight;
    public int bytesPerPixel;
    public int numFramesToBuffer;
    public int headerSize;
    public int imageSize;
    public int fileSize;
    
    
 public override string ToString()
 {
  string res = "{";
  res += ("\"imageWidth\":" + imageWidth + ",\n");
  res += ("\"imageHeight\":" + imageHeight + ",\n");
  res += ("\"bytesPerPixel\":" + bytesPerPixel + ",\n");
  res += ("\"numFramesToBuffer\":" + numFramesToBuffer + ",\n");
  res += ("\"headerSize\":" + headerSize + ",\n");
  res += ("\"imageSize\":" + imageSize + ",\n");
  res += ("\"fileSize\":" + fileSize + "\n");
  res += "}";
  return res;
 }
 
    public CameraCaptureConfig()
    {
        // TODO(KARAN): Find a better way for this.
        headerSize = this.ToByteArray().Length;
    }
    
    public static CameraCaptureConfig CreateFromJSON(string json)
    {
        CameraCaptureConfig result = JsonUtility.FromJson<CameraCaptureConfig>(json);
        result.imageSize = result.bytesPerPixel * result.imageHeight * result.imageWidth;
        result.fileSize = result.imageSize * result.numFramesToBuffer;
        return result;
    }
    
    public static byte[] ConvertIntToByte(int a)
    {
        byte[] result = new byte[4];
        
        result[0] = (byte)(a & 0x000000FF);
        result[1] = (byte)((a >> 8) & 0x000000FF); 
        result[2] = (byte)((a >> 16) & 0x000000FF);
        result[3] = (byte)((a >> 24) & 0x000000FF);
        
        return result;
    }
    
    public byte[] ToByteArray()
    {
        List<byte> serialized = new List<byte>();
        
  serialized.AddRange(ConvertIntToByte(headerSize));
        serialized.AddRange(ConvertIntToByte(imageWidth)) ;
        serialized.AddRange(ConvertIntToByte(imageHeight));
        serialized.AddRange(ConvertIntToByte(bytesPerPixel));
        serialized.AddRange(ConvertIntToByte(numFramesToBuffer));
        serialized.AddRange(ConvertIntToByte(imageSize));
        serialized.AddRange(ConvertIntToByte(fileSize));
        
        return serialized.ToArray();
    }
    
}




        // NOTE(KARAN): Loading JSON file and creating a config object that sets the frame buffer size, image size, etc.
        if (cameraCaptureConfigJsonFilePath.Length == 0)
        {
            cameraCaptureConfigJsonFilePath = pathToNonUnityFolder + "\\Data\\cameraCaptureConfig.json";
        }
        
        string json = File.ReadAllText(cameraCaptureConfigJsonFilePath);
        cameraCaptureConfig = CameraCaptureConfig.CreateFromJSON(json);
        
        
        
*/