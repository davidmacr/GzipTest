# GzipTest

Application created to zip or unzip files using GZipStream class of .NET

The application receive the following parameter:
    • compressing: GZipTest.exe compress [original file name] [archive file name]
    • decompressing: GZipTest.exe decompress [archive file name] [decompressing file name]

The process is done taking advantage of basic multithreading clases, The application uses to semafore class in order to limit the number of concurrent threads, this value is also configurable on application config. 

## For compressing    
    Reads the file in blocks of 4MB (this value is configurable on the application config file), all blocks are compress and save to a common file. 
    The process to save all compressed blocks to common file uses the mutex in order to avoid conflicts during save.
    The file has a header with the information of the blocks as a json:
        Order
        Start index in the file
        Size
    The firts 4 bytes in the file has the lenght of the header information        
    
## For Decompressing
    First read the 4 first bytes to get the size of the header
    Reades and deserialize the json data
    Process in parallel all the blocks saved in the header information, use the mutex class to avoid issues during readings
    Using the ManualResetEvent sincronize the process enabling to save the blocks in order and get correct output
    
## Clases
### ApplicationStarter.cs
    Validates that arguments are valid
    Create a class with the arguments received
    In case of error show the messages:
        If parameters are not valid
        If an error occurs during the execution
        
### ProcessManager.cs
    Creates/Reads the blocks requires for compress or decompress
    Run the parallel execution of each block and control the creation of the final outcome
    Raise error to cancel threads or dispose thread objects
    
### IGZipper.cs
    Interfaz with required methods to compress or decompress files
    
### GZipMain.cs
    Base class with common methods used during compress and decompress files
    
### GZipFiles.cs
    Concret class with specific functions for compress files
    
### UnGZipFiles.cs
    Concret class with specific functions for decompress files
    
### GzipFactory.cs
    Class used to created concret clases of IGzip for compressing or decompressing files
        
## Configuration file
    <add key="fileBlockSize" value="4194304"/> in bytes
    <add key="concurrentBlocks" value="64"/>
    <add key="threadPoolSize" value="7"/>
