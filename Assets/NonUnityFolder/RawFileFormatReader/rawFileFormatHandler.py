import numpy as np
import sys
import cv2
import mmap
import json
import os
import io
import threading
import time
#from PIL import Image
#from PIL import ImageOps

def BytesToInt32(byte0, byte1, byte2, byte3):

    integer = byte0 
    integer = (byte1 << 8) | integer
    integer = (byte2 << 16) | integer
    integer = (byte3 << 24) | integer
    return integer
    
def BytesArrayToInt32(fourBytesArray):
    return BytesToInt32(fourBytesArray[0], fourBytesArray[1], fourBytesArray[2], fourBytesArray[3])

def GetHeaderFromRawBytes(byteData):

    rawHeader = RawHeader()

    rawHeader.magicValue.append(byteData[0])
    rawHeader.magicValue.append(byteData[1])
    rawHeader.magicValue.append(byteData[2])

    index = 3
    rawHeader.versionNumber = BytesArrayToInt32(byteData[index: index + 4])
    index = index + 4

    rawHeader.numImages = BytesArrayToInt32(byteData[index: index + 4])
    index = index + 4

    rawHeader.imageWidth = BytesArrayToInt32(byteData[index: index + 4])
    index = index + 4

    rawHeader.imageHeight = BytesArrayToInt32(byteData[index: index + 4])
    index = index + 4

    rawHeader.bytesPerPixel = BytesArrayToInt32(byteData[index: index + 4])
    index = index + 4

    rawHeader.imageStartOffset = BytesArrayToInt32(byteData[index: index + 4])
    index = index + 4

    assert(index == rawHeader.imageStartOffset)
    return rawHeader

class RawHeader:
    def __init__(self):
        self.magicValue = [];
        self.versionNumber = 0;
        self.numImages = 0;
        self.imageWidth = 0;
        self.imageHeight = 0;
        self.bytesPerPixel = 0;
        self.imageStartOffset = 0;
        
    def ToString(self):
        result = ""
        result = result + "magicValue: " + ''.join(chr(i) for i in self.magicValue) + "\n"
        result = result + "versionNumber: " + str(self.versionNumber) + "\n"
        result = result + "numImages: " + str(self.numImages) + "\n"
        result = result + "imageWidth: " + str(self.imageWidth) + "\n"
        result = result + "imageHeight: " + str(self.imageHeight) + "\n"
        result = result + "bytesPerPixel: " + str(self.bytesPerPixel) + "\n"
        result = result + "imageStartOffset: " + str(self.imageStartOffset) + "\n"
        return result

def BytesToNumpy(imageBytes, width, height, bytesPerPixel):
    imageBytes = np.asarray(imageBytes)
    imageBytes = np.reshape(imageBytes, (width, height, bytesPerPixel))

    if bytesPerPixel == 3:
        option = cv2.COLOR_RGB2BGR
    else:
        option = cv2.COLOR_RGBA2BGR
    imageBytes = cv2.cvtColor(imageBytes, option)
    image = cv2.flip(imageBytes, 0)
    return image

def RawToNumpies(byteData, rawHeader):
    result = []
    numImages = rawHeader.numImages

    for imageNum in range(numImages):
        imageSize = rawHeader.imageWidth * rawHeader.imageHeight * rawHeader.bytesPerPixel

        start = rawHeader.imageStartOffset + (imageSize * imageNum)
        onePastEnd = start + imageSize
        
        imageBytes = byteData[start:onePastEnd]
        image = BytesToNumpy(imageBytes, rawHeader.imageWidth, rawHeader.imageHeight, rawHeader.bytesPerPixel)

        result.append(image)
    return result


def RawToPNGAndSave(byteData, rawHeader, offsetForNaming, imageNumStart, numImages, savingLocation):
    #TODO(KARAN): frameOffset can be encoded into the config. So maybe consider it in future
    
    imageNumEnd = imageNumStart + numImages - 1
    
    assert imageNumStart <= imageNumEnd
    assert imageNumEnd < rawHeader.numImages

    for imageNum in range(imageNumStart, imageNumEnd + 1):
        imageSize = rawHeader.imageWidth * rawHeader.imageHeight * rawHeader.bytesPerPixel

        start = rawHeader.imageStartOffset + (imageSize * imageNum)
        onePastEnd = start + imageSize

        imageBytes = byteData[start:onePastEnd]
        image = BytesToNumpy(imageBytes, rawHeader.imageWidth, rawHeader.imageHeight, rawHeader.bytesPerPixel)
        
        pngFilename = str(offsetForNaming + imageNum) + ".png"
        cv2.imwrite(savingLocation + pngFilename, image)
        
if __name__ == "__main__":

    numArgs = len(sys.argv)
    assert numArgs == 3
    arg = sys.argv[1]
    maxThreads = int(sys.argv[2])
    isDir = os.path.isdir(arg)
    savingLocation = ""
    rawFilenameWithoutExt = ""
    rawFilename = ""
    homeDirectoryPath = ""
    
    if isDir:
        homeDirectoryPath = arg
        homeDirectoryName = os.path.basename(homeDirectoryPath)
        savingLocation = arg + "/" + homeDirectoryName + "_convertedToPNGs/"
    else:
        homeDirectoryPath = os.path.dirname(arg)
        rawFilename = os.path.basename(arg)
        rawFilenameWithoutExt = os.path.splitext(rawFilename)[0]
        savingLocation = homeDirectoryPath + "/" + rawFilenameWithoutExt + "_convertedToPNGs/"

    if not os.path.exists(savingLocation):
        os.makedirs(savingLocation)

    filenamesToConvert = []    
    if isDir:
        for rawFilename in os.listdir(arg):
            if rawFilename[-3:] == "raw":
                filenamesToConvert.append(rawFilename)
    else:
        filenamesToConvert.append(rawFilename)

    numFiles = len(filenamesToConvert)
    
    start = time.time()
    startCopy = start
    globalTotalImagesDone = 0
    for rawFilename in filenamesToConvert:
        print("Saving images at: ", savingLocation)
        print("======================================================================")
        print("Converting ", rawFilename)

        rawFilenameWithoutExt = rawFilename.split(".")[0]
        rawFilePath = os.path.join(homeDirectoryPath, rawFilename)

        fh = open(rawFilePath, 'rb')
        readMemory = mmap.mmap(fh.fileno(), 0, access=mmap.ACCESS_READ)
        byteData = bytearray(readMemory)

        rawHeader = GetHeaderFromRawBytes(byteData)
        print(rawHeader.ToString())
        
        numThreads = min(maxThreads, rawHeader.numImages)
        imagesPerThread = rawHeader.numImages // numThreads

        
        currentImageNum = 0
        threads = []
        for threadNum in range(numThreads):
            imagesForThisThread = imagesPerThread
            if(threadNum == numThreads - 1):
                imagesForThisThread = imagesForThisThread + (rawHeader.numImages % numThreads)

            arguments = (byteData, rawHeader, globalTotalImagesDone, currentImageNum, imagesForThisThread, savingLocation)
            t = threading.Thread(target=RawToPNGAndSave, args=arguments)
            threads.append(t)
            t.start()    
            currentImageNum = currentImageNum + imagesForThisThread
        assert currentImageNum == rawHeader.numImages
        globalTotalImagesDone = globalTotalImagesDone + currentImageNum
        
        
        print("Active threads: ", str(threading.active_count()))
        for t in threads:
            t.join()

        end = time.time()
        print(rawFilename, " conversion took ", str(end - start), " seconds")
        print("======================================================================")
        start = end
        
    end = time.time()
    print("Total time: ", str((end - startCopy)/60), " mins.")
