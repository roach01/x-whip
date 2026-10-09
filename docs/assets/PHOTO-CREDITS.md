# 照片来源与表情编辑

唯一基准照片：Elon Musk close-up (cropped)，Justin R. Pacheco / U.S. Air Force。[Wikimedia Commons 来源页面](https://commons.wikimedia.org/wiki/File:Elon_Musk_close-up_(cropped).jpg)标注为 Public Domain，原图保存在 base-photo.jpg。

v4 的 faces.png 是基于该真实照片通过内置 imagegen 编辑的十六种表情，保留同一正脸视角、暖色打光、发型和年龄；并非十六张不同场景的实拍照片。

4×4 顺序：微笑、困倦、偷笑、怀疑；不爽侧眼、愤怒皱眉、咬牙、怒吼；眨眼、惊讶、大笑、委屈；惊慌、严厉、狂怒、最大怒吼。

程序按主头部轮廓注册统一裁切尺寸，再等比绘制。透明 alpha 查看器中的黑底不是素材背景。图标来自第一格；摄影师与照片来源不表示对本原型的认可。