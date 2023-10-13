# 操作系统模拟实验

该项目为武汉大学《操作系统课程设计》课程内容。

操作系统模拟实验涉及进程调度、虚拟内存管理、文件系统三个模块，旨在通过模拟操作系统的各个核心部分，加深对操作系统运行方式的理解，提高系统编程能力。实验需要创建一个与外界无关的模拟框架，让一切模拟过程都发生在这一虚拟的环境内，并得到和真实操作系统相似的效果。

实验环境：开发工具为 Visual Studio 2022，编程语言为 C#。

测试程序：考虑到实验要求以文字输入输出为主，三个实验均采用 C#.NET 编写控制台应用程序作为测试用的界面，并尽可能优化其可视化程度及使用体验。

## 进程调度实验

本实验模拟在单处理器环境下的进程调度及状态转换，需要设计一个将优先级调度与时间片调度相结合的调度算法，能够实现进程状态的变迁。然后编写合适的测试程序来测试相关功能。

![](Screenshots/Scheduling-1.png)

## 虚拟内存管理试验

本实验模拟请求分页内存管理的功能，包括地址映射、页面置换（clock）、页面的分配与回收等。假设物理内存有 256MB，虚拟内存有 4GB，设计基于分页的虚拟内存管理机制。

![](Screenshots/MemoryManaging-1.png)

![](Screenshots/MemoryManaging-2.png)

![](Screenshots/MemoryManaging-3.png)

## 文件系统

本实验的内容为设计一个简单的两级目录文件系统，包括主目录和用户文件目录，支持列文件目录和文件的创建、删除、打开、关闭、读写等操作，实现链接结构的文件存储，提供类控制台的用户界面。

![](Screenshots/FileSystem-1.png)

![](Screenshots/FileSystem-2.png)

![](Screenshots/FileSystem-3.png)
