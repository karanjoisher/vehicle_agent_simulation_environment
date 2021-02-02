import numpy as np
import sys
import os
import io
import time
import csv
import json

def optimalFramesToBuffer(imageSize, headerSize, discSpeedBytesPS, desiredFPS, safetySeconds):
    return (((safetySeconds * discSpeedBytesPS) + headerSize) * desiredFPS) / (discSpeedBytesPS - (desiredFPS * imageSize))

if __name__ == "__main__":

    
    numArgs = len(sys.argv)
    csvfilenames = []
    for filename in os.listdir(sys.argv[1]):
        if filename[-3:] == "csv":
            csvfilenames.append(filename)

    bufferingSettingsFileHandle = open(sys.argv[1] + "\\bufferingSettings.json", 'r')
    bufferingSettings = json.load(bufferingSettingsFileHandle)
    
    numFramesToBuffer = bufferingSettings['numFramesToBuffer']
    fields = [] 
    rows = []
    
    lagDifferences = []
    leadDifferences = []

    totals = []

    percentages = []
    frameDifferences = []

    timesToFillBuffer = []
    timesToSaveBuffer = []
    
    for filename in csvfilenames:
        
        
        # reading csv file 
        with open(sys.argv[1] + "\\" + filename, 'r') as csvfile: 
            # creating a csv reader object 
            csvreader = csv.reader(csvfile) 
              
            # extracting field names through first row 
            fields = next(csvreader) 
              
            # extracting each data row one by one 
            for row in csvreader: 
                #print(row)
                rows.append(row) 

    for i in range(0, len(rows)):
        timesToFillBuffer.append(float(rows[i][0]))
        timesToSaveBuffer.append(float(rows[i][1]))

    avgTimeToSave = np.mean(timesToSaveBuffer)   
    bufferSize = bufferingSettings['headerSize'] + bufferingSettings['fileSize']
    bytesPerSecond = bufferSize/avgTimeToSave
    
    avgTimeToFill = np.mean(timesToFillBuffer)
    fps = numFramesToBuffer/avgTimeToFill
    print("Average fps: ", fps)
    print("Average saving speed: ", bytesPerSecond, " bytes/s")
    print("Average time to fill: ",avgTimeToFill)
    print("Average time to save: ",avgTimeToSave)
    
    for i in range(0, len(rows)):
        row = rows[i]
        difference = float(row[0]) - float(row[1])
        total = float(row[0]) + float(row[1])
        percentage = (float(row[0])/total) * 100.0

        if(difference < 0):
            lagDifferences.append(abs(difference))
        else:
            leadDifferences.append(difference)

        totals.append(total)
        percentages.append(percentage)


    meanPercentage = np.mean(percentages)
    print(str(len(rows)) + " records analysed")
    print("Filling the buffer takes on avg " + str(meanPercentage) + "% of the total time. (It must be >= 50%)")


    framesToBuffer = optimalFramesToBuffer(bufferingSettings["imageSize"], bufferingSettings["headerSize"], bytesPerSecond, 30.0, 3.0)
    if(len(lagDifferences) > 0):
        meanDifference = np.mean(lagDifferences)    
        print(len(lagDifferences)/len(rows) * 100.0, "% of the total samples show an avg lag of ", meanDifference, " secs")
        print("Suggested frames to buffer: ", framesToBuffer)    

    if(len(leadDifferences) > 0):
        meanDifference = np.mean(leadDifferences)
        print(len(leadDifferences)/len(rows) * 100.0, "% of the total samples show an avg lead of ", meanDifference, " secs") 
        print("(Optional) Suggested frames to buffer: ", framesToBuffer)    
    

