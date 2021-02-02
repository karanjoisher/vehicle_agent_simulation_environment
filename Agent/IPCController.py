import mmap
import random
import sys
#sys.path.append(".././VirtualModelTester/Assets/NonUnityFolder/RawFileFormatReader")
sys.path.append(".././OldEnv/VirtualModelTester/Assets/NonUnityFolder/RawFileFormatReader")
from rawFileFormatHandler import *
import mmap
import numpy as np
import cv2
import csv
import struct
import cnn_model
import tensorflow as tf

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


def velocity_to_vertical_axis(cur_vel, prev_vel, norm_vel):
    if cur_vel < prev_vel:
        norm_vel = norm_vel - 0.1
    elif cur_vel > prev_vel:
        norm_vel = norm_vel + 0.1

    prev_vel = cur_vel

    if norm_vel <= 0 or norm_vel >= 0.85:
        norm_vel = 0.3

    return cur_vel, prev_vel, norm_vel


# def road_positioning(left_pos, right_pos, cur_HA, prev_HA):
#     right_dist = right_pos/road_width
#     left_dist = left_pos/road_width
#     sharp_turn = 0.6  # Change this to control how sharply the car turns when close to road boundaries
#     # Only keep the steering angle if it is above threshold (to keep the car stable)
#     adjusted_HA = cur_HA if abs(prev_HA-cur_HA) > 0.25 else 0
#
#     if right_dist < 0.25 or abs(right_dist) > 1.0:
#         adjusted_HA = adjusted_HA - sharp_turn if adjusted_HA >= -0.2 else adjusted_HA - 0.3
#     elif left_dist < 0.25 or abs(left_dist) > 1.0:
#         adjusted_HA = adjusted_HA + sharp_turn if adjusted_HA <= 0.2 else adjusted_HA + 0.3
#
#     if adjusted_HA > 1.0:
#         adjusted_HA = 1.0
#     elif adjusted_HA < -1.0:
#         adjusted_HA = -1.0
#
#     print("Left: ", left_dist, "Right: ", right_dist,  "HA: ", adjusted_HA)
#
#
#     return adjusted_HA

def road_positioning(left_pos, right_pos, cur_HA, prev_HA):
    '''
    First adjust the steering. Only keep the predicted angle if it is above threshold
    '''
    adjusted_HA = cur_HA if abs(prev_HA-cur_HA) > 0.25 else 0

    '''
    This function is for making sure that the car steers the opposite way if it goes near the end of the lane. 
    Old values: 
    left_pos > 4.5 or right_pos < 2.9
    left_pos < 0.95 or right_pos > 6.5
    '''

    '''
    Driving Test 2.scene = Scene 0 
    Driving Test Scene = Scene 1 
    '''
    if right_pos < 2.5:
        adjusted_HA = -0.6
    elif left_pos < 0.95:
        adjusted_HA = 0.6

    return adjusted_HA




def grayscale_prediction(leftImage, frontImage, rightImage):
    X = []

    # Convert the input images to grayscale and resize
    front = cv2.resize(cv2.cvtColor(frontImage, cv2.COLOR_RGB2GRAY), (w, h))
    left = cv2.resize(cv2.cvtColor(leftImage, cv2.COLOR_RGB2GRAY), (w, h))
    right = cv2.resize(cv2.cvtColor(rightImage, cv2.COLOR_RGB2GRAY), (w, h))

    images = np.stack((front, left, right))  # num_cameras * h * w
    # print(images.shape)
    # Move the axis to make it h * w * num_cameras
    images = np.moveaxis(images, [0, 1, 2], [2, 0, 1])   # For gray scale

    # Test if the data is correct by visualizing it
    # cv2.imshow('test1', images[0, :, :, :])
    # cv2.waitKey(1)

    # Append the images to X, this makes h * w * cameras to 1 * h * w * cameras where 1 = number of samples
    X.append(images)

    return np.array(X)


def color_prediction(leftImage, frontImage, rightImage):
    X = []

    # Convert RGB to YUV/OR change it as per programmer's need and resize
    front = cv2.resize(cv2.cvtColor(frontImage, cv2.COLOR_RGB2YUV), (w, h))
    left = cv2.resize(cv2.cvtColor(leftImage, cv2.COLOR_RGB2YUV), (w, h))
    right = cv2.resize(cv2.cvtColor(rightImage, cv2.COLOR_RGB2YUV), (w, h))

    images = np.stack((front, left, right))  # num_cameras * h * w * channels
    X.append(images)

    return np.array(X)   # samples (1) * cameras * h * w * channels


if __name__ == "__main__":

    # START(KARAN): Initialization of shared memory
    sharedMemoryName = "ipc"
    #filePath = input("Enter path of memory layout csv file(Generally in Assets or in VirtualModelTester_Data folder): ")
    #filePath = ".././VirtualModelTester/Assets/sharedMemoryLayout.csv"
    filePath = ".././VirtualModelTester/Assets/sharedMemoryLayout.csv"
    #filePath = ".././NewEnv/VirtualModelTester/Assets/sharedMemoryLayout.csv"
    print("File Path: ", filePath)

    sharedMemory = Memory(sharedMemoryName, filePath)
    run = True

    # Hyper Parameters
    h, w = 66, 200
    cameras = 3
    channels = 3
    #state_size = [cameras, h, w, channels] # For color
    state_size = [h, w, cameras]
    learning_rate = 1e-5
    classes = 2  # Velocity and Steering
    batch_size = 256
    epochs = 100
    mod_number = 4  # Just to save models with unique names
    prev_vel = 0
    norm_vel = 0.3
    cur_HA = 0
    prev_HA = 0
    counter = -1
    skips = 1
    training = False
    road_width = 7.2

    # Test data
    HA_test = [-1, 0.9, -5, +5, 0]
    VA_test = [-1, 0.9, -5, +5, 0]

    # Load the class instance
    tf.reset_default_graph()
    drivingNet = cnn_model.UNNetwork(state_size=state_size, learning_rate=learning_rate, output_units=classes, training=training)

    with tf.Session() as sess:
        # Load the model
        saver = tf.train.Saver(tf.global_variables())
        saver = tf.train.import_meta_graph(
            "./models/drivingModel_{LR}_{EP}_{MN}.ckpt.meta".format(LR=learning_rate, EP=epochs, MN=mod_number))
        saver.restore(sess, "./models/drivingModel_{LR}_{EP}_{MN}.ckpt".format(LR=learning_rate, EP=epochs,
                                                                               MN=mod_number))
        while run:
            counter += 1
            if (UNITY_IPC_SYNC == 0 or ReadByte(sharedMemory, 'unityFlag') != 0) and counter % skips == 0:
                f = open("debug" + str(frame) + "~", 'wb')
                f.write(sharedMemory.file)
                f.close()
                # NOTE(KARAN): Get car data
                carData = ReadFloatArray(sharedMemory, 'carData')
                #print("CAR DATA: ", carData)


                # NOTE(KARAN): Get left camera image
                leftImage = ReadImage(sharedMemory, 'Left')

                # NOTE(KARAN): Get front camera image
                frontImage = ReadImage(sharedMemory, 'Front')

                # NOTE(KARAN): Get right camera image
                rightImage = ReadImage(sharedMemory, 'Right')

                #X = color_prediction(leftImage, frontImage, rightImage)
                X = grayscale_prediction(leftImage, frontImage, rightImage)

                # Check if X is correct (for color)
                # cv2.imshow('test',  X[0, 0, :, :, :])
                # cv2.waitKey(1)

                # Check if X is correct (for gray scale)
                # cv2.imshow('test', X[0, :, :, 0])
                # cv2.waitKey(1)
                #print(X.shape)

                # final_image = np.hstack((X[0, :, :, 1], X[0, :, :, 0]))
                # final_image = np.hstack((final_image, X[0, :, :, 2]))
                # cv2.imshow('All cameras:', final_image)  # Positioning: Left, Center, Right
                # cv2.waitKey(1)

                # NOTE(KARAN): Upload Neural Network's decision
                decision = sess.run(drivingNet.output, feed_dict={drivingNet.inputs_: X})
                decision_list = decision.tolist()  # This gives nested list
                flat_list = [item for sublist in decision_list for item in sublist]  # Un-nest the list
                #print("Decision OG: ", flat_list)
                cur_vel, prev_vel, norm_vel = velocity_to_vertical_axis(flat_list[0], prev_vel, norm_vel)

                '''
                Final List position 0 = velocity / Vertical Axis 
                Final List position 1 = Steering angle (HA) 
                '''



                if flat_list[1] > 1.0:
                    flat_list[1] = 1.0
                final_list = [norm_vel, flat_list[1]]
                cur_HA = road_positioning(carData[2], carData[3], final_list[1], prev_HA)  # Account for car going offroad
                final_list[1] = cur_HA
                prev_HA = cur_HA
                #print("Car data: ", carData)
                #print("Output: ", final_list)
                # if counter % 500 == 0:
                #     final_list.append(1)
                # else:
                #     final_list.append(0)

                # For testing purposes
                #if VA_test:
                 #   final_list[0] = VA_test.pop(0)

                WriteFloatArray(sharedMemory, 'pythonData', final_list)
                #
                # # NOTE(KARAN): Set python flag to 1, indicating a new decision has been made
                # #              Set unity flag to 0, indicating we used the data that was uploaded by unity
                WriteByte(sharedMemory, 'pythonFlag', 1)
                WriteByte(sharedMemory, 'unityFlag',  0)

            if counter > 1e6:
                counter = 0

    cv2.destroyAllWindows()
