import sys
sys.path.append("..\\RawFileFormatReader")
from rawFileFormatHandler import *
import mmap
import numpy as np
import cv2


if __name__ == "__main__":

    rawFilePath = "W:\\VirtualModelTester\\Assets\\CapturedImages~\\Front\\0.raw"

    fh = open(rawFilePath, 'rb')
    readMemory = mmap.mmap(fh.fileno(), 0, access=mmap.ACCESS_READ)
    byteData = bytearray(readMemory)

    # Extract .raw's configuration
    rawHeader = GetHeaderFromRawBytes(byteData)
    images = RawToNumpies(byteData, rawHeader)

    cv2.namedWindow('images', cv2.WINDOW_NORMAL)
    for image in images:
        cv2.imshow('images', image)
        cv2.waitKey(16)
    cv2.destroyAllWindows()

