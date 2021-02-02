'''
Creating a separate file for training functions. So that any model can be imported straight away without any hassle.
'''

import tensorflow as tf
import numpy as np
import math
import cv2
import cnn_model


def train_model():
    tf.reset_default_graph()  # Clear any existing graph values (To avoid errors)

    # Initialize instance of CNN model
    drivingNet = cnn_model.UNNetwork(state_size=state_size, learning_rate=learning_rate, output_units=classes, training=training)

    # Saver object
    saver = tf.train.Saver()

    # Create summary objects for tensor board (Visualization)
    # Summary of loss function
    loss_train = tf.placeholder(tf.float32, shape=None, name='loss_train')
    loss_train_summary = tf.summary.scalar(name='loss_train_summ', tensor=loss_train)

    # Summary of test loss
    loss_test = tf.placeholder(tf.float32, shape=None, name='loss_test')
    loss_test_summary = tf.summary.scalar(name='loss_test_summ', tensor=loss_test)

    # Combine all summaries
    performance_measures = tf.summary.merge([loss_train_summary, loss_test_summary])

    with tf.Session() as sess:

        # Define summary writer
        summ_writer = tf.summary.FileWriter('./models/summaries', sess.graph)
        sess.run(tf.global_variables_initializer())

        for epoch in range(epochs):
            batch_start = 0
            print("Running Epoch: ", epoch)
            cost = []
            for imgs in range(total_train_batches):

                x_batch = X_train[batch_start:batch_start+batch_size, ]
                y_batch = Y_train[batch_start:batch_start+batch_size, ]
                # # If the relative road position is to be considered
                # y_left = Y_left[batch_start:batch_start+batch_size, ]
                # y_right = Y_right[batch_start:batch_start+batch_size, ]

                batch_start += batch_size

                loss, _ = sess.run([drivingNet.loss, drivingNet.optimizer],
                                   feed_dict={drivingNet.inputs_: x_batch, drivingNet.outputs_: y_batch})

                cost.append(loss)

            # If dataset doesn't split the batches perfectly, run the remaining examples of last truncated batch
            if total_train_examples % batch_size != 0:
                x_batch = X_train[batch_start: batch_start+total_train_examples - (batch_size*total_train_batches), ]
                #print(x_batch.shape)
                y_batch = Y_train[batch_start: batch_start+total_train_examples - (batch_size*total_train_batches), ]

                # y_left = Y_left[batch_start: batch_start+total_train_examples - (batch_size*total_train_batches), ]
                # y_right = Y_right[batch_start: batch_start+total_train_examples - (batch_size*total_train_batches), ]
                #print(y_batch.shape)
                loss, _ = sess.run([drivingNet.loss, drivingNet.optimizer],
                                   feed_dict={drivingNet.inputs_: x_batch, drivingNet.outputs_: y_batch})

                cost.append(loss)

            epoch_loss = np.mean(cost)
            print("Epoch:", epoch, "Train loss: ", epoch_loss)

            # Run the test set and get the test loss
            batch_start = 0
            cost = []

            for imgs in range(total_test_batches):
                x_batch = X_test[batch_start:batch_start + batch_size, ]
                y_batch = Y_test[batch_start:batch_start + batch_size, ]
                # y_left = Y_left_test[batch_start: batch_start + batch_size, ]
                # y_right = Y_right_test[batch_start: batch_start + batch_size, ]
                batch_start += batch_size
                loss = sess.run([drivingNet.loss], feed_dict={drivingNet.inputs_: x_batch, drivingNet.outputs_: y_batch})
                cost.append(loss)

            if total_test_examples % batch_size != 0:
                x_batch = X_test[batch_start: batch_start+total_train_examples - (batch_size*total_test_batches), ]
                y_batch = Y_test[batch_start: batch_start+total_train_examples - (batch_size*total_test_batches), ]
                # y_left = Y_left_test[batch_start: batch_start + total_train_examples - (batch_size * total_test_batches), ]
                # y_right = Y_right_test[batch_start: batch_start + total_train_examples - (batch_size * total_test_batches), ]
                loss = sess.run([drivingNet.loss], feed_dict={drivingNet.inputs_: x_batch, drivingNet.outputs_: y_batch})

                cost.append(loss)

            test_loss = np.mean(cost)

            # Save the summaries to the event file
            cur_summaries = sess.run(performance_measures, feed_dict={loss_train: epoch_loss, loss_test: test_loss})
            summ_writer.add_summary(cur_summaries, epoch)

        # Save the model
        save_path = saver.save(sess, "./models/drivingModel_{LR}_{EP}_{MN}.ckpt".format(LR=learning_rate, EP=epochs, MN=mod_number))
        print("Model saved!")


def train_test_split(X, Y, Y_dist=None, train_size=0.8, randomize=False):
    total_samples = len(X)
    randomized_order = np.random.permutation(total_samples)
    relative_dist = False
    if isinstance(Y_dist, np.ndarray):
        relative_dist = True
    if randomize:
        X_randomized = X[randomized_order, :]
        Y_randomized = Y[randomized_order, :]
        if Y_dist:
            Y_left_randomized = Y_dist[randomized_order, :0]
            Y_right_randomized = Y_dist[randomized_order, :1]

    else:
        X_randomized = X
        Y_randomized = Y
        if relative_dist:
            Y_left_randomized = Y_dist[:, 0]
            Y_right_randomized = Y_dist[:, 1]

    # Get the training samples as per train_size. Train size denotes percentage
    train_limit = math.floor(total_samples*train_size)
    test_limit = total_samples - train_limit
    # print("Total", total_samples)
    # print("Train limit", train_limit)
    # print("Test limit", test_limit)

    X_train = X_randomized[:train_limit, ]
    Y_train = Y_randomized[:train_limit, ].reshape(train_limit, classes)
    if relative_dist:
        # Make sure to reshape the arrays to (size, 1)
        Y_left_train = Y_left_randomized[:train_limit, ].reshape(train_limit, 1)
        Y_right_train = Y_right_randomized[:train_limit, ].reshape(train_limit, 1)
        Y_left_test = Y_left_randomized[train_limit:train_limit+test_limit, ].reshape(test_limit, 1)
        Y_right_test = Y_right_randomized[train_limit:train_limit+test_limit, ].reshape(test_limit, 1)

    X_test = X_randomized[train_limit:train_limit+test_limit, ]
    Y_test = Y_randomized[train_limit:train_limit+test_limit, ].reshape(test_limit, classes)

    return X_train, Y_train, X_test, Y_test


if __name__ == "__main__":

    # Hyper parameters
    h, w = 66, 200
    cameras = 3
    channels = 3
    #state_size = [cameras, h, w, channels]
    state_size = [h, w, cameras]   # This is the state for gray scale images
    learning_rate = 1e-5
    classes = 1  # Velocity and Steering OR VA and Steering or only single attribute depends on the Y data
    batch_size = 256
    epochs = 100
    training = True

    # Load the data
    X = np.load('X7_66_200.npy')  # Samples * h * w * cameras
    Y = np.load('Y7_66_200.npy')  # Samples * class_instances

    #Y_dist = np.load('Y5_dist_66_200.npy')

    # Divide into train and test
    X_train, Y_train, X_test, Y_test = train_test_split(X, Y)

    print(X_train.shape)
    print(X_test.shape)
    print(Y_train.shape)
    print(Y_test.shape)


    # Train and test information
    total_train_examples = len(X_train)
    total_test_examples = len(X_test)
    total_train_batches = math.floor(total_train_examples/batch_size)
    total_test_batches = math.floor(total_test_examples/batch_size)
    mod_number = 10  # Just to save models with unique names

    # Train the model
    train_model()
