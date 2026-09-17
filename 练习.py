# from asyncio import shield
# from functools import total_ordering
# from idlelib import query
# from itertools import count
# from unittest import result

#词典练习
# slang_dict = {"kskbl":"康神开播了",
#               "zdjd":"真的假的"}
# slang_dict["wkzkbl"] = "我靠真开播了"
# slang_dict["mp"] = "没胖"
# slang_dict["wzbqsl"] = "我真比以前瘦了"
# slang_dict["nmzzyswzdysblll"] = "你们再这样说我真的呀受不了嘞"
# slang_dict["bzms"] = "别这么说"
#
# query = input("请输入你要查询的:")
# if query in slang_dict:
#     print("你查询的" +query+ "的含义")
#     print(slang_dict[query])
# else:print("字典中没有您查询的词,我们这里有"+str(len(slang_dict))+"词条")


#for循环练习   1至100的和
# total = 0
#
# for i in range(101):
#     total = total + i
#     print(total)


#while循环
# lsit1 = ["你","好","吗","美","女"]
# i = 0
# while i < len(lsit1):
#     print(lsit1[i])
#     i += 1

#for循环与while循环的区别
#for循环有明确循环的次数和对象
#while循环的循环次数位置，往往是需要达到某种结果的循环

#以下为ai生成
# num_sum = 0   # 保存数字总和
# count = 0     # 记录输入数字个数
#
# while True:
#     user_input = input("请输入数字，输入q结束：")
#     # 判断是否退出
#     if user_input == "q":
#         break
#     # 转换成数字
#     try:
#         number = float(user_input)
#         num_sum += number
#         count += 1
#     except ValueError:
#         print("输入无效，请输入数字或者q！")
#
# # 计算平均值，防止没有输入数字报错
# if count > 0:
#     average = num_sum / count
#     print(f"一共输入{count}个数字，平均值为：{average}")
# else:
#     print("你没有输入任何数字！")



#求平均值的while循环练习
# user_math = input("请输入一些数字(完成所有数字输入后请按q终止输入):")
# total = 0
# count = 0
# while user_math != "q":
#     if user_math.strip() == "":                                        #if的这一段是我提出的想法让ai增加的
#         print("不能输入空白，请重新输入数字！")                              #原因：在运行时出现不小心手滑点两次回车键导致程序判定输入了空字符，出现报错
#         user_math = input("请输入一些数字(完成所有数字输入后请按q终止输入):")
#         continue
#
#     num = float(user_math)
#     total += num
#     count += 1
#     user_math = input("请输入一些数字(完成所有数字输入后请按q终止输入):")
#
# if count == 0:
#    result = 0
#    print("你没有输入有效数字")
# else:
#     result = total /count
# print("您输出的数字平均值为" + str(result))





# def calculate_sector(central_angle, radius):
#     sector_area_1 = central_angle / 360 * 3.14 * radius ** 2
#     print(f"此扇形面积为：{sector_area_1}")
# calculate_sector(160,30)


# def calculate_sector(central_angle, radius):
#     area = central_angle / 360 * 3.14 * radius ** 2
#     print(f"此扇形面积为：{area}\n")
#
# # 循环持续接收输入
# while True:
#     try:
#         angle = float(input("请输入扇形圆心角度数："))
#         r = float(input("请输入扇形半径："))
#         calculate_sector(angle, r)
#     except ValueError:
#         print("输入错误！请输入数字\n")


# def colculate_BMI(BMI_weight,BMI_height):
#     print("您的BMI值为:",area)
# colculate_BMI(180,1.8)

#计算BMI（ai生成）
# def calculate_BMI(BMI_weight, BMI_height):
#     area = BMI_weight / (BMI_height ** 2)
#     if area <= 18.5:
#         classify = "偏瘦"
#     elif area <= 25:
#         classify = "正常"
#     elif area <= 30:
#         classify = "偏胖"
#     else:
#         classify = "肥胖"
#     print(f"您的BMI分类为：{classify}")
#     print(f"您的BMI值为：{area}")
#     return area
#
# calculate_BMI(90, 1.8)

#计算扇形面积的函数
# def calculate_sector_1():
#     central_angle_1 = 160
#     radius_1 = 30
#     sector_area_1 = central_angle_1 / 360 * 3.14 * radius_1 ** 2
#     print(f"此扇形的面积为:{sector_area_1}")

# def sector_area(angle, radius):
#     return angle / 360 * 3.14 * radius ** 2
#
# result_list = []
# print("多组扇形面积计算，每组输入：角度,半径；输入 q 结束输入")
#
# while True:
#     user_input = input("请输入一组数据(角度,半径)：")
#     if user_input.lower() == 'q':
#         break
#     try:
#         ang_str, r_str = user_input.split(',')
#         ang = float(ang_str.strip())
#         r = float(r_str.strip())
#         s = sector_area(ang, r)
#         result_list.append({"角度": ang, "半径": r, "面积": round(s,2)})
#     except:
#         print("格式错误！示例：90,5\n")
#
# print("\n===== 全部计算结果 =====")
# for item in result_list:
#     print(f"角度:{item['角度']:>6}  半径:{item['半径']:>4}  面积:{item['面积']:>8}")

# class Student:
#     def __init__(self, name, student_id):
#         self.name = name
#         self.student_id = student_id
#         self.grades = {"语文":0,"数学":0,"英语":0}
#
# def set_grade(self,course,grade):
#     if course in self.grades:
#        self.grades[course] = grade
#
# chen = Student("小陈","114514")
# kang = Student("抗神","231321")
# print(chen.name)
# print(chen.student_id)
# print(chen.grades)

