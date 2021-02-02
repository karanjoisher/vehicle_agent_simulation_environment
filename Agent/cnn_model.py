import tensorflow as tf
import numpy as np
import math
import cv2

# End to End deep learning network structure with max-pooling layers. Roughly based on Nvidia model.
class EENetwork:
    def __init__(self, state_size, learning_rate, output_units, name='EENetwork'):
        self.learning_rate = learning_rate
        self.state_size = state_size    # State size =  height * width * cameras
        self.output_units = output_units

        with tf.variable_scope(name):
            self.inputs_ = tf.placeholder(tf.float32, [None, *state_size], name='inputs')
            self.outputs_ = tf.placeholder(tf.float32, [None, self.output_units], name='outputs_')  # To hold the training Y


            # Placeholders aren't added to the saved graph by default
            tf.add_to_collection('inputs', self.inputs_)
            tf.add_to_collection('outputs_', self.outputs_)

            # Conv1 = samples * h * w * 24
            # Assume h, w = 64, so samples * 64 * 64 * 24
            self.conv1 = tf.layers.conv2d(inputs=self.inputs_, filters=24, kernel_size=[5, 5],
                                          padding='same', activation=tf.nn.elu, name='conv1')



            # Maxpool1 = samples * h/2 * w/2 * 12 = samples * 32 * 32 * 24
            self.maxpool1 = tf.layers.max_pooling2d(inputs=self.conv1, pool_size=[2, 2], strides=2, name='mp1')

            # Conv2 = samples * 32 * 32* 36
            self.conv2 = tf.layers.conv2d(inputs=self.maxpool1, filters=36, kernel_size=[5, 5],
                                          padding='same', activation=tf.nn.elu, name='conv2')

            # Maxpool2 = samples * 16 * 16 * 36
            self.maxpool2 = tf.layers.max_pooling2d(inputs=self.conv2, pool_size=[2, 2], strides=2, name='mp2')

            # Conv3 = samples * 16 * 16 * 48
            self.conv3 = tf.layers.conv2d(inputs=self.maxpool2, filters=48, kernel_size=[3, 3],
                                          padding='same', activation=tf.nn.elu, name='conv3')

            # Maxpool3 = samples * 8 * 8 * 48
            self.maxpool3 = tf.layers.max_pooling2d(inputs=self.conv3, pool_size=[2, 2], strides=2, name='mp3')

            # Conv4 = samples * 8 * 8 * 64
            self.conv4 = tf.layers.conv2d(inputs=self.maxpool3, filters=64, kernel_size=[3, 3],
                                          padding='same', activation=tf.nn.elu, name='conv4')

            # Maxpool4 = samples * 4 * 4 * 64
            self.maxpool4 = tf.layers.max_pooling2d(inputs=self.conv4, pool_size=[2, 2], strides=2, name='mp4')

            # Adding dropout to prevent over-fitting
            self.dropped = tf.nn.dropout(self.maxpool4, keep_prob=0.5)

            # Flatten = samples * 1024
            self.flatten = tf.contrib.layers.flatten(self.dropped)

            # FC1, Output units  = 1164
            self.fc1 = tf.layers.dense(inputs=self.flatten, units=1164, activation=tf.nn.elu,
                                       kernel_initializer=tf.contrib.layers.xavier_initializer(), name='fc1')

            # FC2, Output units = 100
            self.fc2 = tf.layers.dense(inputs=self.fc1, units=100, activation=tf.nn.elu,
                                       kernel_initializer=tf.contrib.layers.xavier_initializer(), name='fc2')

            # FC3, Output units = 50
            self.fc3 = tf.layers.dense(inputs=self.fc2, units=50, activation=tf.nn.elu,
                                       kernel_initializer=tf.contrib.layers.xavier_initializer(), name='fc3')

            # FC4, Output units = 10
            self.fc4 = tf.layers.dense(inputs=self.fc3, units=10, activation=tf.nn.elu,
                                       kernel_initializer=tf.contrib.layers.xavier_initializer(), name='fc4')

            # Output size = given by user, for non-regressive models
            # self.output = tf.layers.dense(inputs=self.fc4, units=self.output_units, activation=tf.nn.softmax,
            #                            kernel_initializer=tf.contrib.layers.xavier_initializer())

            self.output = tf.layers.dense(inputs=self.fc4, units=self.output_units, name='DL')

            # Cost
            #self.loss = tf.reduce_mean(tf.nn.softmax_cross_entropy_with_logits_v2(logits=self.output, labels=self.outputs_))
            self.loss = tf.losses.mean_squared_error(labels=self.outputs_, predictions=self.output)

            # Optimizer
            self.optimizer = tf.train.AdamOptimizer(self.learning_rate).minimize(self.loss)


# End to end deep learning architecture without maxpooling. A combination of Unity and Nvidia model.
#    Convolution: 5x5, filter: 24, strides: 2x2, activation: ELU
#     Convolution: 5x5, filter: 36, strides: 2x2, activation: ELU
#     Convolution: 5x5, filter: 48, strides: 2x2, activation: ELU
#     Convolution: 3x3, filter: 64, strides: 1x1, activation: ELU
#     Convolution: 3x3, filter: 64, strides: 1x1, activation: ELU
#     Drop out (0.5)
#     Fully connected: neurons: 100, activation: ELU
#     Fully connected: neurons: 50, activation: ELU
#     Fully connected: neurons: 10, activation: ELU
#     Fully connected: neurons: 1 (output)
class UNNetwork:
    def __init__(self, state_size, learning_rate, output_units, training, name='UNNetwork'):
        self.learning_rate = learning_rate
        self.state_size = state_size    # State size =  height * width * cameras
        self.output_units = output_units
        self.training = training

        with tf.variable_scope(name):
            self.inputs_ = tf.placeholder(tf.float32, [None, *state_size], name='inputs')
            self.outputs_ = tf.placeholder(tf.float32, [None, self.output_units], name='outputs_')  # To hold the training Y

            self.conv1 = tf.layers.conv2d(inputs=self.inputs_, filters=24, kernel_size=[5, 5],
                                          strides=(2, 2), padding='valid', activation=tf.nn.elu, name='conv1')

            self.conv2 = tf.layers.conv2d(inputs=self.conv1, filters=36, kernel_size=[5, 5], strides=(2, 2),
                                          padding='valid', activation=tf.nn.elu, name='conv2')

            self.conv3 = tf.layers.conv2d(inputs=self.conv2, filters=48, kernel_size=[5, 5], strides=(2, 2),
                                          padding='valid', activation=tf.nn.elu, name='conv3')

            self.conv4 = tf.layers.conv2d(inputs=self.conv3, filters=64, kernel_size=[3, 3], strides=(1, 1),
                                          padding='valid', activation=tf.nn.elu, name='conv4')

            self.conv5 = tf.layers.conv2d(inputs=self.conv4, filters=64, kernel_size=[3, 3], strides=(1, 1),
                                          padding='valid', activation=tf.nn.elu, name='conv5')

            self.dropped = tf.layers.dropout(self.conv5, rate=0.5, training=self.training)  # Set training to False while testing

            self.flatten = tf.layers.flatten(self.dropped)

            self.fc1 = tf.layers.dense(inputs=self.flatten, units=100, activation=tf.nn.elu,
                                       kernel_initializer=tf.contrib.layers.xavier_initializer(), name='fc1')

            self.fc2 = tf.layers.dense(inputs=self.fc1, units=50, activation=tf.nn.elu,
                                       kernel_initializer=tf.contrib.layers.xavier_initializer(), name='fc2')

            self.fc3 = tf.layers.dense(inputs=self.fc2, units=10, activation=tf.nn.elu,
                                       kernel_initializer=tf.contrib.layers.xavier_initializer(), name='fc3')

            self.output = tf.layers.dense(inputs=self.fc3, units=self.output_units, name='DL')

            self.loss = tf.losses.mean_squared_error(labels=self.outputs_, predictions=self.output)

            self.optimizer = tf.train.AdamOptimizer(self.learning_rate).minimize(self.loss)


class coloredNetwork:
    def __init__(self, state_size, learning_rate, output_units, training, name='UNNetwork'):
        self.learning_rate = learning_rate
        self.state_size = state_size    # State size =  height * width * cameras
        self.output_units = output_units
        self.training = training

        with tf.variable_scope(name):
            self.inputs_ = tf.placeholder(tf.float32, [None, *state_size], name='inputs')
            self.outputs_ = tf.placeholder(tf.float32, [None, self.output_units], name='outputs_')  # To hold the training Y

            self.conv1 = tf.layers.conv3d(inputs=self.inputs_, filters=24, kernel_size=[1, 5, 5],
                                          strides=(2, 2, 2), padding='valid', activation=tf.nn.elu, name='conv1')

            self.conv2 = tf.layers.conv3d(inputs=self.conv1, filters=36, kernel_size=[1, 5, 5], strides=(1, 2, 2),
                                          padding='valid', activation=tf.nn.elu, name='conv2')

            self.conv3 = tf.layers.conv3d(inputs=self.conv2, filters=48, kernel_size=[1, 5, 5], strides=(1, 2, 2),
                                          padding='valid', activation=tf.nn.elu, name='conv3')

            self.conv4 = tf.layers.conv3d(inputs=self.conv3, filters=64, kernel_size=[1, 3, 3], strides=(1, 1, 1),
                                          padding='valid', activation=tf.nn.elu, name='conv4')

            self.conv5 = tf.layers.conv3d(inputs=self.conv4, filters=64, kernel_size=[1, 3, 3], strides=(1, 1, 1),
                                          padding='valid', activation=tf.nn.elu, name='conv5')

            self.dropped = tf.layers.dropout(self.conv5, rate=0.5, training=self.training)  # Set training to False while testing

            self.flatten = tf.layers.flatten(self.dropped)

            self.fc1 = tf.layers.dense(inputs=self.flatten, units=100, activation=tf.nn.elu,
                                       kernel_initializer=tf.contrib.layers.xavier_initializer(), name='fc1')

            self.fc2 = tf.layers.dense(inputs=self.fc1, units=50, activation=tf.nn.elu,
                                       kernel_initializer=tf.contrib.layers.xavier_initializer(), name='fc2')

            self.fc3 = tf.layers.dense(inputs=self.fc2, units=10, activation=tf.nn.elu,
                                       kernel_initializer=tf.contrib.layers.xavier_initializer(), name='fc3')

            self.output = tf.layers.dense(inputs=self.fc3, units=self.output_units, name='DL')

            self.loss = tf.losses.mean_squared_error(labels=self.outputs_, predictions=self.output)

            self.optimizer = tf.train.AdamOptimizer(self.learning_rate).minimize(self.loss)


class UNNetwork2:
    def __init__(self, state_size, learning_rate, output_units, training, name='UNNetwork2'):
        self.learning_rate = learning_rate
        self.state_size = state_size    # State size =  height * width * cameras
        self.output_units = output_units
        self.training = training

        with tf.variable_scope(name):
            self.inputs_ = tf.placeholder(tf.float32, [None, *state_size], name='inputs')
            self.outputs_ = tf.placeholder(tf.float32, [None, self.output_units], name='outputs_')  # To hold the training Y

            # For the left distance and right distances from the end points of the road
            self.distLeft = tf.placeholder(tf.float32, [None, 1], name='distLeft')
            self.distRight = tf.placeholder(tf.float32, [None, 1], name='distRight')

            self.conv1 = tf.layers.conv2d(inputs=self.inputs_, filters=24, kernel_size=[5, 5],
                                          strides=(2, 2), padding='valid', activation=tf.nn.elu, name='conv1')

            self.conv2 = tf.layers.conv2d(inputs=self.conv1, filters=36, kernel_size=[5, 5], strides=(2, 2),
                                          padding='valid', activation=tf.nn.elu, name='conv2')

            self.conv3 = tf.layers.conv2d(inputs=self.conv2, filters=48, kernel_size=[5, 5], strides=(2, 2),
                                          padding='valid', activation=tf.nn.elu, name='conv3')

            self.conv4 = tf.layers.conv2d(inputs=self.conv3, filters=64, kernel_size=[3, 3], strides=(1, 1),
                                          padding='valid', activation=tf.nn.elu, name='conv4')

            self.conv5 = tf.layers.conv2d(inputs=self.conv4, filters=64, kernel_size=[3, 3], strides=(1, 1),
                                          padding='valid', activation=tf.nn.elu, name='conv5')

            self.dropped = tf.layers.dropout(self.conv5, rate=0.5, training=self.training)  # Set training to False while testing

            self.flatten = tf.layers.flatten(self.dropped)

            self.fc1 = tf.layers.dense(inputs=self.flatten, units=100, activation=tf.nn.elu,
                                       kernel_initializer=tf.contrib.layers.xavier_initializer(), name='fc1')

            self.fc2 = tf.layers.dense(inputs=self.fc1, units=50, activation=tf.nn.elu,
                                       kernel_initializer=tf.contrib.layers.xavier_initializer(), name='fc2')

            self.fc3 = tf.layers.dense(inputs=self.fc2, units=10, activation=tf.nn.elu,
                                       kernel_initializer=tf.contrib.layers.xavier_initializer(), name='fc3')

            self.fc4 = tf.layers.dense(inputs=self.fc3, units=3, activation=tf.nn.elu,
                                       kernel_initializer=tf.contrib.layers.xavier_initializer(), name='fc4')

            # Combine the three units obtained by analysing the images with left and right distances
            self.fc5 = tf.concat([self.fc4, self.distLeft, self.distRight], axis=1)

            self.output = tf.layers.dense(inputs=self.fc5, units=self.output_units, name='DL')

            self.loss = tf.losses.mean_squared_error(labels=self.outputs_, predictions=self.output)

            self.optimizer = tf.train.AdamOptimizer(self.learning_rate).minimize(self.loss)












