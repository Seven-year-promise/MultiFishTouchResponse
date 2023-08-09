### Data Acquisition Platform for Touch-Response Behavior Screening of Zebrafish

- Second paper link: https://ieeexplore.ieee.org/stamp/stamp.jsp?arnumber=9647931
- First paper link: https://ieeexplore.ieee.org/stamp/stamp.jsp?tp=&arnumber=9521519

### This code is implemented by C#, and uses the following libraries:

- OpenCvSharp (the opencv library for C#: https://github.com/shimat/opencvsharp)
- Tensorflow (the tensorflow library for C#: https://github.com/SciSharp/TensorFlow.NET)
- NumSharp (numpy library for C#: https://github.com/SciSharp/NumSharp)

### Additionally comments that are used for debugging:

- Some useful notes on Touch response development

    c# call functionf from python

    install IronPython

- Compile python scripts to *.dll:

    install pyinstaller $ pip install pyinstaller

    `pyinstaller test.py`

if error "module 'enum' has no attribute 'IntFlag'" $ pip uninstall -y enum34

### In the case of citing our work

```
@ARTICLE{9647931,
  author={Wang, Yanke and Kanagaraj, Naveen Krishna and Pylatiuk, Christian and Mikut, Ralf and Peravali, Ravindra and Reischl, Markus},
  journal={IEEE Robotics and Automation Letters}, 
  title={High-Throughput Data Acquisition Platform for Multi-Larvae Touch-Response Behavior Screening of Zebrafish}, 
  year={2022},
  volume={7},
  number={2},
  pages={858-865},
  doi={10.1109/LRA.2021.3134281}}
```

```
@ARTICLE{9521519,
  author={Wang, Yanke and Marcato, Daniel and Tirumalasetty, Vani and Kanagaraj, Naveen Krishna and Pylatiuk, Christian and Mikut, Ralf and Peravali, Ravindra and Reischl, Markus},
  journal={IEEE Transactions on Automation Science and Engineering}, 
  title={An Automated Experimentation System for the Touch-Response Quantification of Zebrafish Larvae}, 
  year={2022},
  volume={19},
  number={4},
  pages={3007-3019},
  doi={10.1109/TASE.2021.3104507}}
```
