import mysql.connector

def convertToBinaryData(filename):
    with open(filename, 'rb') as file:
        binaryData = file.read()
    return binaryData


def insertBLOB(id, photo):
    print("Inserting BLOB into python_employee table")
    try:
        connection = mysql.connector.connect(host='localhost',
                                             database='neko',
                                             user='root',
                                             password='root')

        cursor = connection.cursor()
        sql_insert_blob_query = """ INSERT INTO neko_blobs VALUES (%s,%s)"""

        image = convertToBinaryData(photo)
        insert_blob_tuple = (id, image)
        result = cursor.execute(sql_insert_blob_query, insert_blob_tuple)
        connection.commit()
        print("Image and file inserted successfully as a BLOB into python_employee table", result)

    except mysql.connector.Error as error:
        print("Failed inserting BLOB data into MySQL table {}".format(error))

    finally:
        if connection.is_connected():
            cursor.close()
            connection.close()
            print("MySQL connection is closed")

insertBLOB(0, r'C:\Users\user\Desktop\ph\PutinOnBear.png')
