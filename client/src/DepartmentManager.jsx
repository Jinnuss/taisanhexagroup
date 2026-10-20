import React, {
  useEffect,
  useState
} from "react";

import {
  createDepartment,
  deleteDepartment,
  getDepartments,
  updateDepartment
} from "./services/departmentService";

const emptyForm = {
  departmentCode: "",
  departmentName: "",
  description: ""
};

function DepartmentManager({
  currentUser,
  onBack
}) {
  const [departments, setDepartments] = useState([]);
  const [form, setForm] = useState(emptyForm);
  const [editingId, setEditingId] = useState(null);
  const [message, setMessage] = useState("");

  async function loadDepartments() {
    try {
      const data = await getDepartments();
      setDepartments(data);
    } catch (error) {
      setMessage(
        error.response?.data?.message ||
        "Không tải được phòng ban."
      );
    }
  }

  useEffect(() => {
    loadDepartments();
  }, []);

  function handleChange(event) {
    setForm({
      ...form,
      [event.target.name]: event.target.value
    });
  }

  async function handleSubmit(event) {
    event.preventDefault();

    try {
      if (editingId) {
        await updateDepartment(editingId, form);
        setMessage("Cập nhật phòng ban thành công.");
      } else {
        await createDepartment(form);
        setMessage("Thêm phòng ban thành công.");
      }

      setEditingId(null);
      setForm(emptyForm);
      await loadDepartments();
    } catch (error) {
      setMessage(
        error.response?.data?.message ||
        "Không thể lưu phòng ban."
      );
    }
  }

  function handleEdit(department) {
    setEditingId(department.Id);

    setForm({
      departmentCode:
        department.DepartmentCode || "",

      departmentName:
        department.DepartmentName || "",

      description:
        department.Description || ""
    });
  }

  async function handleDelete(id) {
    if (
      !window.confirm(
        "Bạn có chắc chắn muốn xóa phòng ban?"
      )
    ) {
      return;
    }

    try {
      await deleteDepartment(id);
      setMessage("Xóa phòng ban thành công.");
      await loadDepartments();
    } catch (error) {
      setMessage(
        error.response?.data?.message ||
        "Không thể xóa phòng ban."
      );
    }
  }

  return (
    <main className="container">
      <header>
        <div>
          <p className="eyebrow">HEXA GROUP</p>
          <h1>Quản lý phòng ban</h1>
        </div>

        <button onClick={onBack}>
          Quay lại tài sản
        </button>
      </header>

      {currentUser.role === "admin" && (
        <form
          className="card"
          onSubmit={handleSubmit}
        >
          <h2>
            {editingId
              ? "Cập nhật phòng ban"
              : "Thêm phòng ban"}
          </h2>

          <div className="grid">
            <label>
              Mã phòng ban
              <input
                name="departmentCode"
                value={form.departmentCode}
                onChange={handleChange}
                required
              />
            </label>

            <label>
              Tên phòng ban
              <input
                name="departmentName"
                value={form.departmentName}
                onChange={handleChange}
                required
              />
            </label>
          </div>

          <label>
            Mô tả
            <textarea
              name="description"
              value={form.description}
              onChange={handleChange}
              rows="3"
            />
          </label>

          <div className="actions">
            <button type="submit">
              {editingId
                ? "Lưu thay đổi"
                : "Thêm phòng ban"}
            </button>

            {editingId && (
              <button
                type="button"
                className="muted"
                onClick={() => {
                  setEditingId(null);
                  setForm(emptyForm);
                }}
              >
                Hủy
              </button>
            )}
          </div>
        </form>
      )}

      {message && (
        <p className="message">{message}</p>
      )}

      <section className="card">
        <h2>Danh sách phòng ban</h2>

        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Mã</th>
                <th>Tên phòng ban</th>
                <th>Mô tả</th>
                <th>Số tài sản</th>
                {currentUser.role === "admin" && (
                  <th>Thao tác</th>
                )}
              </tr>
            </thead>

            <tbody>
              {departments.map((department) => (
                <tr key={department.Id}>
                  <td className="code">
                    {department.DepartmentCode}
                  </td>

                  <td>
                    {department.DepartmentName}
                  </td>

                  <td>
                    {department.Description || "-"}
                  </td>

                  <td>{department.AssetCount}</td>

                  {currentUser.role === "admin" && (
                    <td>
                      <div className="row-actions">
                        <button
                          onClick={() =>
                            handleEdit(department)
                          }
                        >
                          Sửa
                        </button>

                        <button
                          className="danger"
                          onClick={() =>
                            handleDelete(department.Id)
                          }
                        >
                          Xóa
                        </button>
                      </div>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </main>
  );
}

export default DepartmentManager;