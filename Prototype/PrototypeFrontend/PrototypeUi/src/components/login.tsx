import { useState, type FC } from "react";
import { useForm, type SubmitHandler } from "react-hook-form";
import useLoginStore from "../stores/login-store";
import { useNavigate } from "react-router";

type Login = {
  email: string;
  password: string;
};

const Login: FC = () => {
  const [showWrongPassord, setShowWrongPassword] = useState(false);
  const {
    register,
    formState: { errors },
    handleSubmit,
  } = useForm<Login>();
  const { tryLogin } = useLoginStore();
  const navigate = useNavigate();

  const onLogin: SubmitHandler<Login> = (data) => {
    if (tryLogin(data.email, data.password)) {
      navigate("/");
      return;
    }
    setShowWrongPassword(true);
  };

  return (
    <div className="flex h-full w-full flex-col items-center justify-center">
      <div className="flex h-1/2 min-h-100 w-1/5 min-w-110 flex-col items-center gap-10 rounded-4xl bg-lime-600 p-10">
        <h1 className="text-4xl font-bold">Welcome</h1>
        <form
          className="flex flex-col items-center gap-2"
          onSubmit={handleSubmit(onLogin)}
        >
          {showWrongPassord && (
            <div className="text-red-600">incorrect Pasword or Email</div>
          )}
          <div className="flex flex-col gap-1">
            <label htmlFor="email">Email</label>
            <input
              className="bg-white px-2"
              placeholder="email"
              type="email"
              {...register("email", {
                required: true,
                pattern: /^[\w\-.]+@([\w-]+\.)+[\w-]{2,}$/,
              })}
              aria-invalid={errors.email ? "true" : "false"}
            />
            {errors.email && <div>incorrect email format</div>}
          </div>
          <div className="flex flex-col gap-1">
            <label htmlFor="password">Password</label>
            <input
              className="bg-white px-2"
              placeholder="password"
              type="password"
              {...register("password", { required: true })}
            />
          </div>
          <button
            type="submit"
            className="cursor-pointer rounded-2xl disabled:cursor-default"
          >
            Login
          </button>
        </form>
      </div>
    </div>
  );
};

export default Login;
