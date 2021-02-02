import tensorflow as tf
from collections import deque
from keras.layers import Dense
from keras.optimizers import Adam
from keras.models import Sequential
from keras.initializers import normal, identity
from keras.models import model_from_json
from keras.models import Sequential
from keras.layers.core import Dense, Dropout, Activation, Flatten
from keras.layers.convolutional import Convolution2D, MaxPooling2D
from keras.layers import Conv2D
import numpy as np
import random
from keras import backend as K
import sys
sys.path.append(".././VirtualModelTester/Assets/NonUnityFolder/RawFileFormatReader")
from rawFileFormatHandler import *
import shared_memory_architecture
import cv2
import matplotlib.pyplot as plt
import pickle


# Global parameters
episodes = 10000
h, w = 66, 200
channels = 4  # Stack of 4 black and white images


class DQNetwork():
    def __init__(self, state_size, action_size, discount_factor, learning_rate, train=True):
        self.t = 0
        self.max_Q = 0
        self.state_size = state_size
        self.action_size = action_size
        self.train = train  # When true, we set have exploration turned on

        # DQN Hyper parameters
        self.discount_factor = discount_factor
        self.learning_rate = learning_rate
        if self.train:
            self.epsilon = 1.0
            self.initial_epsilon = 1.0
        else:
            self.epsilon = 1e-6
            self.initial_epsilon = 1e-6

        self.epsilon_min = 0.02
        self.batch_size = 128
        self.train_start = 100
        self.explore = 10000

        # Replay memory
        self.memory = deque(maxlen=3000)

        # Create main model and target model
        self.model = self.build_model()
        self.target_model = self.build_model()

        # In Double DQN, we use two models, target and original
        self.update_target_model()

    def build_model(self):
        model = Sequential()
        model.add(Conv2D(strides=(4, 4), kernel_size=(8, 8), filters=32, padding='same',
                                input_shape=(h, w, channels)))  # 66*200*4
        model.add(Activation('relu'))
        model.add(Conv2D(filters=64, kernel_size=(4, 4), strides=(2, 2), padding='same'))
        model.add(Activation('relu'))
        model.add(Conv2D(filters=64, kernel_size=(3, 3), strides=(1, 1), padding='same'))
        model.add(Activation('relu'))
        model.add(Flatten())
        model.add(Dense(512))
        model.add(Activation('relu'))

        # 15 categorical bins for Steering angles
        model.add(Dense(15, activation="linear"))

        adam = Adam(lr=self.learning_rate)
        model.compile(loss='mse', optimizer=adam)
        print("Model Build Finished")

        return model

    def update_target_model(self):
        self.target_model.set_weights(self.model.get_weights())

    # Get action from model using epsilon-greedy policy
    def get_action(self, s_t):
        if np.random.rand() <= self.epsilon:
            return np.random.uniform(-1, 1)
        else:
            q_value = self.model.predict(s_t)
            # Convert q array to steering value
            return linear_unbin(q_value[0])

    def replay_memory(self, state, action, reward, next_state, done):
        self.memory.append((state, action, reward, next_state, done))
        if self.epsilon > self.epsilon_min:
            #self.epsilon *= self.epsilon_decay
            self.epsilon -= (self.initial_epsilon - self.epsilon_min) / self.explore

    def train_replay(self):
        if len(self.memory) < self.train_start:
            return

        batch_size = min(self.batch_size, len(self.memory))
        minibatch = random.sample(self.memory, batch_size)

        state_t, action_t, reward_t, state_t1, terminal = zip(*minibatch)
        state_t = np.concatenate(state_t)
        state_t1 = np.concatenate(state_t1)
        targets = self.model.predict(state_t)
        self.max_Q = np.max(targets[0])
        target_val = self.model.predict(state_t1)
        target_val_ = self.target_model.predict(state_t1)
        for i in range(batch_size):
            if terminal[i]:
                targets[i][action_t[i]] = reward_t[i]
            else:
                a = np.argmax(target_val[i])
                targets[i][action_t[i]] = reward_t[i] + self.discount_factor * (target_val_[i][a])
        self.model.train_on_batch(state_t, targets)

    def load_model(self, name):
        self.model.load_weights(name)

    # Save the model which is under training
    def save_model(self, name):
        self.model.save_weights(name)


def linear_unbin(arr):
    """
    Convert a categorical array to value.
    """
    if not len(arr) == 15:
        raise ValueError('Illegal array length, must be 15')
    b = np.argmax(arr)
    a = b * (2 / 14) - 1
    return a


def linear_bin(a):
    """
    Convert a value to a categorical array.
    Parameters
    ----------
    a : int or float
        A value between -1 and 1
    Returns
    -------
    list of int
        A list of length 15 with one item set to 1, which represents the linear value, and all other items set to 0.
    """
    a = a + 1   # Steering angle shouldn't be negative when indexing to an array
    b = round(a / (2 / 14))
    arr = np.zeros(15)
    arr[int(b)] = 1
    return arr


def grayscale_prediction(stack):
    frames = []
    X = []

    for frame in stack:
        front = cv2.resize(cv2.cvtColor(frame, cv2.COLOR_RGB2GRAY), (w, h))
        frames.append(front)

    frames = np.moveaxis(np.array(frames), [0, 1, 2], [2, 0, 1])
    X.append(frames)

    return np.array(X)

def road_positioning(left_pos, right_pos, cur_HA, prev_HA):
    '''
    First adjust the steering. Only keep the predicted angle if it is above threshold
    '''
    adjusted_HA = cur_HA if abs(prev_HA-cur_HA) > 0.25 else 0

    if right_pos < 2.5:
        adjusted_HA = -0.6
    elif left_pos < 0.95:
        adjusted_HA = 0.6

    return adjusted_HA


if __name__ == '__main__':
    sess = tf.Session()
    K.set_session(sess)
    # Get size of state and action from environment
    state_size = (h, w, channels)
    action_size = 1  # Only the steering angle is to be predicted
    agent = DQNetwork(state_size, action_size, discount_factor=0.99, learning_rate=1e-4, train=False)
    velocity = 0.3  # Fix the velocity
    road_width = 8
    tolerance = 7
    continue_train = False

    if not agent.train:
        print("Now we load the saved model")
        agent.load_model("./rl_models/dqnModel.h5")

    if continue_train:
        print("Loading saved moved for continued training")
        agent.load_model("./rl_models/dqnModel.h5")


    # Shared memory parameters
    # Access the shared memory
    sharedMemoryName = "ipc"
    filePath = ".././VirtualModelTester/Assets/sharedMemoryLayout.csv"
    sharedMemory = shared_memory_architecture.Memory(sharedMemoryName, filePath)
    counter = 1
    frames = 5  # Number of frames we require + 1
    stack_of_frames = []
    all_rewards = []
    prev_reward = 0

    for ep in range(episodes):
        # print("Episode number: ", ep)
        done = False
        episode_len = 0  # This parameter keeps track of how well the car can drive (longer the better)
        reset = 0
        rewards = []
        cur_HA = 0
        prev_HA = 0
        off_track = 0

        while not done:
            if shared_memory_architecture.UNITY_IPC_SYNC == 0 or\
                    shared_memory_architecture.ReadByte(sharedMemory, 'unityFlag') != 0:
                f = open("debug" + str(shared_memory_architecture.frame) + "~", 'wb')
                f.write(sharedMemory.file)
                f.close()

                # Get car data
                carData = shared_memory_architecture.ReadFloatArray(sharedMemory, 'carData')
                '''
                carData[0] = Velocity 
                carData[1] = Steering 
                carData[2] = Left Dist 
                carData[3] = Right Dist 
                carData[4] = Reward 
                carData[5] = Angular Velocity 
                
                pytonData[0] = velocity
                pythonData[1] = steering 
                pythonData[2] = 1 to reset env, 0 to continue 
                '''
                # Only do something after counter%frames number of frames have been gathered. Till then, keep collecting.
                if counter % frames == 0:
                    # print("Frames captured", len(stack_of_frames))
                    s_t = grayscale_prediction(stack_of_frames)  # Shape = 1*66*200*4 or 1*h*w*stack_of_frames
                    # print("Shape: ", x_t.shape)
                    steering = agent.get_action(s_t)

                    # Road Positioning
                    if not agent.train:
                        cur_HA = road_positioning(carData[2], carData[3], steering,  prev_HA)
                        # print("Steering: ", steering, " New: ", cur_HA)
                        steering = cur_HA
                        prev_HA = cur_HA

                    action = [velocity, steering, reset]

                    # Send prediction to environment
                    shared_memory_architecture.WriteFloatArray(sharedMemory, 'pythonData', action)
                    # Set python flag to 1, indicating a new decision has been made
                    # Set unity flag to 0, indicating we used the data that was uploaded by unity
                    shared_memory_architecture.WriteByte(sharedMemory, 'pythonFlag', 1)
                    shared_memory_architecture.WriteByte(sharedMemory, 'unityFlag', 0)

                    # Get the next state
                    f = open("debug" + str(shared_memory_architecture.frame) + "~", 'wb')
                    f.write(sharedMemory.file)
                    f.close()
                    carData = shared_memory_architecture.ReadFloatArray(sharedMemory, 'carData')
                    x_t1 = shared_memory_architecture.ReadImage(sharedMemory, 'Front')  # Next state
                    new_stack = [x_t1, x_t1, x_t1, x_t1]
                    s_t1 = grayscale_prediction(new_stack)
                    reward = carData[4]
                    rewards.append(reward)
                    # Calculate whether the car has gone off track by a large margin
                    left_dist, right_dist = carData[2], carData[3]

                    if agent.train and (reward == -100 and prev_reward != -100):
                        done = True
                        reset = 1
                        prev_reward = -100
                    elif reward != -100:
                        prev_reward = reward

                    # if left_dist > road_width + tolerance or right_dist > road_width + tolerance:
                    #     off_track += 1
                    #     # print("Off Track", off_track)
                    # if int(carData[0]) == 0:
                    #     off_track += 1
                    # if agent.train and (off_track >= 50 or counter >= 5000 or reward == -100):
                    #     done = True
                    #     reset = 1
                    #     counter = 0
                    #     prev_reward = reward
                    # elif not agent.train and counter >= 100000:
                    #     done = True
                    #     reset = 1
                    #     counter = 0

                    # Add to replay memory
                    agent.replay_memory(s_t, np.argmax(linear_bin(steering)), reward, s_t1, done)

                    # Play the replay memory during training process
                    if agent.train:
                        agent.train_replay()

                    # Empty the stack of frames
                    stack_of_frames = []

                    agent.t = agent.t + 1
                    episode_len = episode_len + 1
                    # if agent.t % 100 == 0:
                    #     print("Episode", ep, " timestep: ", agent.t, " action", action, "/ reward: ", reward,
                    #           "/ Episode len: ", episode_len, "/ Q_MAX ", agent.max_Q)
                else:
                    frontImage = shared_memory_architecture.ReadImage(sharedMemory, 'Front')
                    stack_of_frames.append(frontImage)

                if done:
                    agent.update_target_model()
                    # Save model for each episode
                    if agent.train:
                        agent.save_model("./rl_models/dqnModel.h5")

                    sum_reward = sum(rewards)
                    all_rewards.append(sum_reward)

                    print("episode:", ep, "  memory length:", len(agent.memory),
                          "  epsilon:", agent.epsilon, " episode length:", episode_len, "reward :", sum_reward)

                    # Reset the environment
                    shared_memory_architecture.WriteFloatArray(sharedMemory, 'pythonData', [0, 0, reset])
                    # Set python flag to 1, indicating a new decision has been made
                    # Set unity flag to 0, indicating we used the data that was uploaded by unity
                    shared_memory_architecture.WriteByte(sharedMemory, 'pythonFlag', 1)
                    shared_memory_architecture.WriteByte(sharedMemory, 'unityFlag', 0)

                counter += 1
                if counter % 1000 == 0:
                    print("Current time step: ", counter, " Off track count", off_track)

    # Save the all_rewards list
    with open('./rl_models/rewards', 'wb') as f:
        pickle.dump(all_rewards, f)









