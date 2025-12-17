# Dream Class – Lớp học Thực tế ảo (VR)

## Giới thiệu

**Dream Class** là một hệ thống trò chơi giáo dục ứng dụng công nghệ **Thực tế ảo (Virtual Reality – VR)**, được phát triển bằng **Unity** và **Meta XR SDK**. Dự án hướng tới việc xây dựng một **môi trường học tập tương tác nhập vai**, nơi học sinh tiếp cận kiến thức thông qua phương pháp **Học tập qua trò chơi (Gamification-based Learning)**.

Hệ thống cho phép mô phỏng không gian lớp học quen thuộc của Việt Nam trong môi trường 3D, kết hợp các hoạt động học tập, thực hành và đánh giá nhằm nâng cao hiệu quả tiếp thu kiến thức.

**Video demo:** (đang cập nhật)

---

## Công nghệ sử dụng

* **Game Engine:** Unity `6000.0.60f1`
* **XR SDK:** Meta XR All-in-One SDK
* **Back-end:** NodeJS
* **Database:** MongoDB
* **Front-end:** ReactJS + Ant Design
* **Phân phối nội dung:** Unity Cloud Content Delivery (CCD), Cloudinary

---

## Kiến trúc hệ thống

Dream Class được thiết kế theo mô hình **Client–Server đa nền tảng**, bao gồm 3 module chính:

### 1. Module VR Client (Ứng dụng Thực tế ảo)

* Nền tảng: Unity + Meta XR SDK
* Đối tượng sử dụng: Học sinh
* Chức năng chính:

  * Hiển thị toàn bộ môi trường 3D (Sảnh chờ, Phòng học, Phòng thực hành, Phòng thi, Phòng thành tích)
  * Xử lý tương tác thời gian thực (Grab, Drop, Throw, Rotate)
  * Quản lý logic trò chơi: nhiệm vụ, mini-quiz, thực hành mô phỏng
  * Quản lý tài nguyên động bằng **Unity Addressables** nhằm tối ưu hiệu năng thiết bị VR

### 2. Module Front-End Website (Cổng thông tin Web)

* Công nghệ: ReactJS, Ant Design
* Giao diện đa nền tảng
* Phân quyền người dùng:

  * **Admin:** Quản lý người dùng, cấu hình phần thưởng, kho dữ liệu bài học
  * **Giáo viên:** Theo dõi tiến độ học tập, quản lý lớp học, ngân hàng câu hỏi
  * **Học sinh (Web View):** Xem thành tích, bảng xếp hạng, quản lý hồ sơ cá nhân

### 3. Module Back-End (Máy chủ và Dữ liệu)

* Công nghệ: NodeJS, MongoDB
* Chuẩn giao tiếp: RESTful API
* Chức năng:

  * Xác thực và phân quyền người dùng bằng JWT
  * Lưu trữ dữ liệu học tập, điểm số, nhiệm vụ
  * Phân phối nội dung:

    * Tài liệu PDF: Cloudinary
    * Tài nguyên 3D / AssetBundle: Unity CCD

---

## Thiết kế trò chơi

### Tổng quan

* Thể loại: **Mô phỏng giáo dục (Educational Simulation)** kết hợp **Nhập vai (Role-Playing)**
* Góc nhìn: Thứ nhất (First-Person Perspective – FPP)
* Triết lý thiết kế: **Lấy người học làm trung tâm**, chuyển từ học thụ động sang **Học qua hành động (Learning by Doing)**

### Phong cách đồ họa

* Phong cách: Low-poly
* Định hướng: Đơn giản, tối ưu hiệu năng, phù hợp thiết bị VR độc lập
* Không gian: Mô phỏng lớp học Việt Nam, tạo cảm giác gần gũi, an toàn

---

## Cơ chế điều khiển và tương tác

### Di chuyển (Locomotion)

* Teleportation (mặc định): Giảm say chuyển động
* Smooth Locomotion (tùy chọn): Dành cho người dùng VR nâng cao

### Tương tác vật lý

* Mô phỏng tay ảo 6DoF
* Hành động: Grab, Release, Throw, Rotate
* Phản hồi xúc giác (Haptic Feedback)

### Giao diện người dùng trong không gian (Spatial UI)

* Không sử dụng HUD 2D cố định
* UI hiển thị trong không gian 3D hoặc Wrist UI

---

## Quy trình gameplay

1. **Khởi tạo & Định danh**

   * Người chơi xuất hiện tại Sảnh chờ
   * Đăng nhập thông qua bảng nhập liệu ảo

2. **Tiếp thu kiến thức (Phòng học)**

   * Đọc sách giáo khoa 3D
   * Tương tác NPC giáo viên
   * Thực hiện Mini-Quiz

3. **Thực hành mô phỏng (Phòng thực hành)**

   * Nhận nhiệm vụ
   * Thực hiện thí nghiệm theo kịch bản

4. **Đánh giá & Tổng kết**

   * Phòng Thi: Kiểm tra lý thuyết + thực hành
   * Phòng Thành tích: Xem leaderboard, vòng quay may mắn

---

## Hệ thống trò chơi hóa (Gamification)

* **Tiền tệ:** Vàng (Gold), Điểm (DP)
* **Nhiệm vụ:** Chính tuyến, nhiệm vụ hằng ngày
* **Danh hiệu & vật phẩm:** Banner, Title, phần thưởng sự kiện

---

## Cơ chế đánh giá và tính điểm

* Phương pháp đánh giá: **Tỷ lệ hoàn thành (Completion Rate)**
* Hỗ trợ nhiều mức độ khó:

  * Cơ bản: Không phạt khi sai
  * Thử thách: Áp dụng cơ chế trừ tỷ lệ hoàn thành
* Phần thưởng quy đổi:

  * DP: Xếp hạng học lực
  * Gold: Tiền tệ trong game

---

## Thế giới game

* Bối cảnh: Trường học Việt Nam
* Các khu vực chính:

  * Phòng chờ
  * Phòng học
  * Phòng thực hành
  * Phòng thi
  * Phòng thành tích

---

## Định hướng phát triển

* Mở rộng thêm phòng học theo môn học
* Cá nhân hóa lộ trình học tập
* Tích hợp AI hỗ trợ đánh giá và phản hồi
* Tối ưu hiệu năng cho thiết bị VR độc lập

