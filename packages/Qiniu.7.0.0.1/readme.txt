1.编译qiniu.dll，将它复制到lib/{framework}/下面({framework}可取Net20,Net40等)
2.修改qiniu.dll.nuspec中的相关参数
3.执行nuget pack Qiniu.dll.nuspec
4.执行nuget push Qiniu.dll.<x.x.x>.nupkg （<x.x.x>是本次发布的版本号，的qiniu.dll.nuspec中指定）

apk-key a6f05f8d-2ed3-47ea-8d2d-b80fa0d0bc52