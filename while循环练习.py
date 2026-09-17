# -*- coding: utf-8 -*-
"""
while 循环练习程序

使用方法：
1. 用 PyCharm 打开本文件所在文件夹。
2. 打开 while循环练习.py。
3. 点击右上角运行按钮，按照提示练习。
"""


def pause():
    """暂停，方便在 PyCharm 运行窗口中查看结果。"""
    input("\n按回车键返回主菜单...")


def exercise_count_up():
    """练习 1：从 1 数到用户输入的数字。"""
    print("\n【练习 1】从 1 数到你输入的数字")

    while True:
        text = input("请输入一个正整数：")
        if text.isdigit() and int(text) > 0:
            end_number = int(text)
            break
        print("输入错误，请输入大于 0 的整数。")

    number = 1
    while number <= end_number:
        print(number)
        number += 1

    print("练习完成：这里使用 while number <= end_number 控制循环。")
    pause()


def exercise_count_down():
    """练习 2：倒计时。"""
    print("\n【练习 2】倒计时")

    while True:
        text = input("请输入倒计时开始数字：")
        if text.isdigit() and int(text) > 0:
            number = int(text)
            break
        print("输入错误，请输入大于 0 的整数。")

    while number > 0:
        print(number)
        number -= 1

    print("时间到！")
    print("练习完成：这里使用 while number > 0 控制倒计时。")
    pause()


def exercise_guess_number():
    """练习 3：猜数字小游戏。"""
    print("\n【练习 3】猜数字小游戏")
    answer = 7
    guess_count = 0

    while True:
        text = input("请猜一个 1 到 10 之间的整数：")
        if not text.isdigit():
            print("请输入整数。")
            continue

        guess = int(text)
        guess_count += 1

        if guess < answer:
            print("猜小了。")
        elif guess > answer:
            print("猜大了。")
        else:
            print(f"恭喜你，猜对了！你一共猜了 {guess_count} 次。")
            break

    print("练习完成：这里使用 while True + break 控制游戏是否结束。")
    pause()


def exercise_sum_numbers():
    """练习 4：不断输入数字并求和，输入 q 结束。"""
    print("\n【练习 4】输入多个数字求和")
    print("提示：输入 q 可以结束。")

    total = 0
    count = 0

    while True:
        text = input("请输入一个数字：")

        if text.lower() == "q":
            break

        try:
            number = float(text)
        except ValueError:
            print("输入无效，请输入数字或 q。")
            continue

        total += number
        count += 1
        print(f"当前已经输入 {count} 个数字，总和是 {total}")

    print(f"最终结果：你输入了 {count} 个数字，总和是 {total}")
    print("练习完成：这里使用 while True 持续接收输入。")
    pause()


def exercise_password():
    """练习 5：密码验证，最多输入 3 次。"""
    print("\n【练习 5】密码验证")
    password = "123456"
    chance = 3

    while chance > 0:
        user_password = input("请输入密码：")

        if user_password == password:
            print("登录成功！")
            break

        chance -= 1
        print(f"密码错误，你还剩 {chance} 次机会。")

    if chance == 0:
        print("次数用完，登录失败。")

    print("练习完成：这里使用 while chance > 0 限制循环次数。")
    pause()


def show_menu():
    """显示主菜单。"""
    print("\n========== while 循环练习程序 ==========")
    print("1. 从 1 数到指定数字")
    print("2. 倒计时")
    print("3. 猜数字小游戏")
    print("4. 输入多个数字求和")
    print("5. 密码验证")
    print("0. 退出程序")
    print("======================================")


def main():
    """主程序：使用 while 循环让菜单重复显示。"""
    while True:
        show_menu()
        choice = input("请选择练习编号：")

        if choice == "1":
            exercise_count_up()
        elif choice == "2":
            exercise_count_down()
        elif choice == "3":
            exercise_guess_number()
        elif choice == "4":
            exercise_sum_numbers()
        elif choice == "5":
            exercise_password()
        elif choice == "0":
            print("程序已退出，继续加油练习 Python！")
            break
        else:
            print("选择无效，请重新输入。")


if __name__ == "__main__":
    main()
