            // Packet Definition:
            // 1st byte is packet type, 1 = translation, 2 = rotation, 3 = buttons
            // Packet type 1:
            // b[1] = X translation (0-255 | 0 - 255)
            // b[2] = X translation dir and multiplier. (254,255 L|R 0,1)
            // b[3] = Y translation (0-255 | 0 - 255)
            // b[4] = Y translation dir and multiplier. (254,255 F|B 0,1)
            // b[5] = Z translation (0-255 | 0 - 255)
            // b[6] = Z translation dir and multiplier. (254,255 U|D 0,1)
            // Packet type 2:
            // b[1] = X rotation (0-255 | 0 - 255)
            // b[2] = X rotation dir and multiplier. (254,255 L|R 0,1)
            // b[3] = Y rotation (0-255 | 0 - 255)
            // b[4] = Y rotation dir and multiplier. (254,255 F|B 0,1)
            // b[5] = Z rotation (0-255 | 0 - 255)
            // b[6] = Z rotation dir and multiplier. (254,255 U|D 0,1)
            // Packet type 3:
            // b[1] = Buttons, Left >> 1, Right >> 2
