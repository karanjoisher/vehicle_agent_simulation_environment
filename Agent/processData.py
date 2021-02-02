import numpy as np
import cv2
import os
import re
from keras.utils import to_categorical
import sys
sys.path.append(".././VirtualModelTester/Assets/NonUnityFolder/RawFileFormatReader")
from rawFileFormatHandler import *
import mmap
import pandas as pd
import random
import math

car_path = '.././VirtualModelTester/Assets/CapturedImages~/carControls.csv'
front_cam = '.././VirtualModelTester/Assets/CapturedImages~/Front'
left_cam = '.././VirtualModelTester/Assets/CapturedImages~/Left'
right_cam = '.././VirtualModelTester/Assets/CapturedImages~/Right'


def sorted_natural(data):
    '''
    Function for natural sorting
    :param data:
    :return: Will return a sorted list in natural ordering.
    I.E Instead of 1, 10, 2, 3 it will return 1, 2, 3, 10 etc
    '''

    convert = lambda text: int(text) if text.isdigit() else text.lower()
    alphanum_key = lambda key: [convert(c) for c in re.split('([0-9]+)', key)]

    return sorted(data, key=alphanum_key)


def get_one_hot(targets, nb_classes):
    res = np.eye(nb_classes)[np.array(targets).reshape(-1)]
    return res.reshape(list(targets.shape)+[nb_classes])


def normalize_data(data):

    max_val = 25
    min_val = 0
    data_norm = 0

    # Min max normalization
    if isinstance(data, pd.Series):
        data_norm = data.copy()

        max_val = data.max()
        min_val = data.min()

        data_norm = (data - min_val)/(max_val - min_val)

    return data_norm


def unskew(left, front, right, Y, indices):
    '''
    The dataset will have a large number of entries with steering angle 0.
    Hence, this skews the data. So, we reduce the number of such entries.
    :return: unskewed data
    '''

    front_trimmed = []
    left_trimmed = []
    right_trimmed = []
    Y_trimmed = []

    # First we will select a subset of the indices
    random.shuffle(indices)
    percentage_selected = 0.5  # Example: 0.5 means we remove 50% of the data
    new_len = math.floor(len(indices)*percentage_selected)
    indices_trimmed = indices[:new_len]  # Entries in indices_trimmed won't be considered

    total_length = len(front)
    for i in range(total_length):
        if i not in indices_trimmed:
            front_trimmed.append(front[i])
            left_trimmed.append(left[i])
            right_trimmed.append(right[i])
            Y_trimmed.append(Y[i])

    return front_trimmed, left_trimmed, right_trimmed, Y_trimmed


def load_data_png(resize=True):
    front_images = []
    left_images = []
    right_images = []
    h, w = 64, 64

    Y = []  # List containing the output key strokes of all images

    # Get the strokes output for the front directory
    # The key strokes will be same for all camera positions (front, left, right) so only get it once
    # That is, three images (front, left, right) correspond to one output key stroke
    for file in sorted_natural(os.listdir(front_cam)):
        if file[-3:] == 'txt':
            temp = [int(output_key.strip()) for output_key in open(front_cam+file)]
            Y.extend(temp)

    # Now load the front, left and right images in their respective lists
    image_path = front_cam+'Front_convertedToPNGs/'
    for file in sorted_natural(os.listdir(image_path)):
        fimg = cv2.imread(image_path+file, 0)  # The 0 specifies that image is read as grayscale
        if resize:
            fimg = cv2.resize(fimg, (w, h))
        front_images.append(fimg)

    image_path = left_cam+'Left_convertedToPNGs/'
    for file in sorted_natural(os.listdir(image_path)):
        limg = cv2.imread(image_path + file, 0)  # The 0 specifies that image is read as grayscale
        if resize:
            limg = cv2.resize(limg, (w, h))
        left_images.append(limg)

    image_path = right_cam+'Right_convertedToPNGs/'
    for file in sorted_natural(os.listdir(image_path)):
        rimg = cv2.imread(image_path + file, 0)  # The 0 specifies that image is read as grayscale
        if resize:
            rimg = cv2.resize(rimg, (w, h))
        right_images.append(rimg)

    # To categorical is used to one-hot encode Y
    return np.array(front_images), np.array(left_images), np.array(right_images), np.array(to_categorical(Y)),


def load_data_raw(cam_dir, h=66, w=200, color=False, resize=True):
    images_dir = []

    # Convert front camera .raw files to numpy
    for file in sorted_natural(os.listdir(cam_dir)):
        if file[-3:] == 'raw':
            fh = open(cam_dir + '/' + file, 'rb')
            readMemory = mmap.mmap(fh.fileno(), 0, access=mmap.ACCESS_READ)
            byteData = bytearray(readMemory)

            # Extract .raw's configuration
            rawHeader = GetHeaderFromRawBytes(byteData)
            images = RawToNumpies(byteData, rawHeader)

            # Resize and process the images
            for img in images:
                if not color:
                    img_gray = cv2.cvtColor(img, cv2.COLOR_RGB2GRAY)
                else:
                    img_gray = cv2.cvtColor(img, cv2.COLOR_RGB2YUV)  # Convert to YUV

                if resize:
                    img_gray = cv2.resize(img_gray, (w, h))

                #img_gray = img_gray/255  # Normalize the image
                images_dir.append(img_gray)

    return images_dir


def get_data(color=False, resize=True):
    front_images = []
    left_images = []
    right_images = []
    h, w = 64, 64
    counter = 0  # For naming images

    # Convert front camera .raw files to numpy
    front_images = load_data_raw(front_cam, color=color, resize=resize)
    print("Front images processed!")

    # Convert left camera .raw files to numpy
    left_images = load_data_raw(left_cam, color=color, resize=resize)
    print("Left images processed!")

    # Convert right camera .raw files to numpy
    right_images = load_data_raw(right_cam, color=color, resize=resize)
    print("Right images processed!")

    # print(len(front_images))
    # print(len(left_images))
    # print(len(right_images))

    steering_data, indices_of_zeros = get_car_data()
    front_images, left_images, right_images, y_trimmed = unskew(left=left_images, front=front_images, right=right_images, Y=steering_data, indices=indices_of_zeros)

    return np.array(front_images), np.array(left_images), np.array(right_images), np.array(y_trimmed)


def get_car_data():
    df = pd.read_csv(car_path)
    #velocity = df['Velocity']
    steering = df['Steering']

    # This gets the index values of every entry with steering angle = 0
    indices_of_zeros = steering[steering == 0].index.tolist()

    #leftDist = df['LeftDistance']
    #RightDist = df['RightDistance']

    return steering.tolist(), indices_of_zeros


def visualize_data(X, num_cameras=3):
    # Only works if images are de-normalized.
    total_images = 1000  # Change this number to visualize more data

    for i in range(total_images):
        final_image = np.hstack((X[i, :, :, 1], X[i, :, :, 0]))
        final_image = np.hstack((final_image, X[i, :, :, 2]))
        cv2.imshow('All cameras:', final_image)  # Positioning: Left, Center, Right
        cv2.waitKey(100)

    cv2.destroyAllWindows()

def visualize_data_color(X, num_cameras=3):
    total_images = 1000  # Change this number to visualize more data

    for i in range(total_images):
        final_image = np.hstack((X[i, 1, :, :, :], X[i, 0, :, :, :]))
        final_image = np.hstack((final_image, X[i, 2, :, :, :]))
        cv2.imshow('All cameras:', final_image)  # Positioning: Left, Center, Right
        cv2.waitKey(50)

    cv2.destroyAllWindows()


if __name__ == '__main__':
    # #Process X data
    # front, left, right, Y = get_data(color=False)
    # X = np.stack((front, left, right))
    # print(X.shape) # For gray scale: cameras * samples * h * w
    # # Axis move for gray scale images
    # X = np.moveaxis(X, [0], [3])  # Samples * h *  w * num_cameras
    # print("X shape: ", X.shape)
    # print("Y shape: ", Y.shape)
    # print(len(X))
    # print(len(Y))
    #
    # np.save('X7_66_200.npy', X)
    # np.save('Y7_66_200.npy', Y)
    # #
    # cv2.imshow('front', X[10, 0, :, :, :])
    # cv2.waitKey(0)
    # cv2.imshow('left', X[10, 1, :, :, :])
    # cv2.waitKey(0)
    # cv2.imshow('right', X[10, 2, :, :, :])
    # cv2.waitKey(0)

    # cv2.imshow('front', X[5, :, :, 0])
    # cv2.waitKey(0)
    # cv2.imshow('left', X[5, :, :, 1])
    # cv2.waitKey(0)
    # cv2.imshow('right', X[5, :, :, 2])
    # cv2.waitKey(0)

    # Process Y data
    #velocity, steering, leftDistance, rightDistance = get_car_data()
    #Y = np.vstack((velocity, steering))
    #Y = np.moveaxis(Y, [0], [1])  # To make it samples * values
    #print(Y.shape)
    #Y_dist = np.vstack((leftDistance, rightDistance))
    #Y_dist = np.moveaxis(Y_dist, [0], [1])
    #

    X = np.load('X6_YUV_66_200.npy')
    visualize_data_color(X)







