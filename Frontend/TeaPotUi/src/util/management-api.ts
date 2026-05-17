import axios from "axios";

export const changePassword = async (
  userId: string,
  email: string,
  newPassword: string,
) => {
  const { status } = await axios.patch(
    `${import.meta.env.VITE_API_BASE_URL}/api/user/${userId}/profile/password`,
    { email: email, password: newPassword },
  );

  return status >= 200 && status < 300;
};
