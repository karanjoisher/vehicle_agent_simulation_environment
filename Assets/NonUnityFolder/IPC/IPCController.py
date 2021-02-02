import mmap
import random
import sys
sys.path.append("..\\RawFileFormatReader")
from rawFileFormatHandler import *
import mmap
import numpy as np
import cv2
import csv
import struct

UNITY_IPC_SYNC = 1 # NOTE(KARAN): SET THIS TO 0 IF NO SYNC IS REQUIRED
START = 0
SIZE = 1
frame = 0

class Memory:
    def __init__(self, memoryName, layoutFile):
        self.layout = {}
        sections = []
        self.memorySize = 0
        
        memoryLayoutDescriptorHandle = open(layoutFile, 'r')
        memoryLayoutDescriptor = csv.reader(memoryLayoutDescriptorHandle)

        columnNames = next(memoryLayoutDescriptor)

        print("Memory Layout:")
        print(columnNames)

        for section in memoryLayoutDescriptor:
            sections.append(section) 

        for i in range(0, len(sections)):
            print(sections[i][0], " | ", sections[i][1], " | ", sections[i][2])
            self.memorySize += int(sections[i][2])
            self.layout[sections[i][0]] = [int(sections[i][1]), int(sections[i][2])]

        self.file = mmap.mmap(-1, self.memorySize, memoryName)

def BytesToInt32(byte0, byte1, byte2, byte3):
    integer = byte0 
    integer = (byte1 << 8) | integer
    integer = (byte2 << 16) | integer
    integer = (byte3 << 24) | integer
    return integer

def BytesArrayToInt32(fourBytesArray):
    return BytesToInt32(fourBytesArray[0], fourBytesArray[1], fourBytesArray[2], fourBytesArray[3])

def ReadInt(memory, sectionName):
    fourBytes = bytearray(memory.file[memory.layout[sectionName][START] : memory.layout[sectionName][START] + 4]) 
    return BytesArrayToInt32(fourBytes)

def ReadByte(memory, sectionName):
    return memory.file[memory.layout[sectionName][START]]

def WriteByte(memory, sectionName, value):
    memory.file[memory.layout[sectionName][START]] = value

def ReadByteArray(memory, sectionName):
    return memory.file[memory.layout[sectionName][START] : memory.layout[sectionName][START] + memory.layout[sectionName][SIZE]]


def ReadFloatArray(memory, sectionName):
    result = []
    sizeOfFloat = 4
    sizeOfArray = memory.layout[sectionName][SIZE]
    byteArray = ReadByteArray(memory, sectionName)
    numFloats = sizeOfArray // sizeOfFloat
    for i in range(0, numFloats):
        start = i * 4
        packedFloat = byteArray[start : start + 4]
        result.append(struct.unpack('f', packedFloat)[0])
    return result

def WriteFloatArray(memory, sectionName, floatArray):
    sectionOnePastEnd = memory.layout[sectionName][START] +  memory.layout[sectionName][SIZE]
    for i in range(0, len(floatArray)):
        packedFloat = struct.pack('f', floatArray[i])
        start = memory.layout[sectionName][START] + (i * 4)
        assert(start + 4 <= sectionOnePastEnd)
        memory.file[start : start + 4] = packedFloat



def WriteByteArray(memory, sectionName, source):
    memory.file[memory.layout[sectionName][START] : memory.layout[sectionName][START] + memory.layout[sectionName][SIZE]] = source


def ReadImage(memory, sectionName):

    imageWidth = ReadInt(memory, sectionName + 'ImageWidth')
    imageHeight = ReadInt(memory, sectionName + 'ImageHeight')
    bytesPerPixel = ReadInt(memory, sectionName + 'BytesPerPixel')
    
    imageStart = memory.layout[sectionName][START]
    imageSize  = memory.layout[sectionName][SIZE]
    imageByteData = bytearray(memory.file[imageStart: imageStart + imageSize])

    return BytesToNumpy(imageByteData, imageWidth, imageHeight, bytesPerPixel)

    
if __name__ == "__main__":

    # START(KARAN): Initialization of shared memory
    sharedMemoryName = "ipc"
    #filePath = input("Enter path of memory layout csv file(Generally in Assets or in VirtualModelTester_Data folder): ")
    filePath = "../../sharedMemoryLayout.csv"
    sharedMemory = Memory(sharedMemoryName, filePath)
    run = True
    while run:
        if UNITY_IPC_SYNC == 0 or ReadByte(sharedMemory, 'unityFlag') != 0:
            f = open("debug" + str(frame) + "~", 'wb')
            f.write(sharedMemory.file)
            f.close()
            # NOTE(KARAN): Get car data
            carData = ReadFloatArray(sharedMemory, 'carData')
            print("CAR DATA: ", carData)

            # NOTE(KARAN): Get left camera image
            leftImage = ReadImage(sharedMemory, 'Left')

            # NOTE(KARAN): Get front camera image
            frontImage = ReadImage(sharedMemory, 'Front')

            # NOTE(KARAN): Get right camera image
            rightImage = ReadImage(sharedMemory, 'Right')

            images = np.concatenate((leftImage, frontImage), axis=1)
            images = np.concatenate((images, rightImage), axis=1)

            cv2.imshow("Display", images)
            cv2.waitKey(1)

            # NOTE(KARAN): Upload Neural Network's decision
            neuralNetworkRandomDecision = [5.5, -0.3]
            WriteFloatArray(sharedMemory, 'pythonData', neuralNetworkRandomDecision)

            # NOTE(KARAN): Set python flag to 1, indicating a new decision has been made
            #              Set unity flag to 0, indicating we used the data that was uploaded by unity
            WriteByte(sharedMemory, 'pythonFlag', 1)
            WriteByte(sharedMemory, 'unityFlag',  0)
    
    cv2.destroyAllWindows()
